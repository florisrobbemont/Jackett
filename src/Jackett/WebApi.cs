﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Jackett
{
    public class WebApi
    {
        static string WebContentFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebContent");
        static string[] StaticFiles = Directory.EnumerateFiles(WebContentFolder, "*", SearchOption.AllDirectories).ToArray();
        public Server server;

        public enum WebApiMethod
        {
            GetConfigForm,
            ConfigureIndexer,
            GetIndexers,
            TestIndexer,
            DeleteIndexer,
            GetSonarrConfig,
            ApplySonarrConfig,
            TestSonarr,
            GetJackettConfig,
            ApplyJackettConfig,
            JackettRestart,
        }

        static Dictionary<string, WebApiMethod> WebApiMethods = new Dictionary<string, WebApiMethod> {
			{ "get_config_form", WebApiMethod.GetConfigForm },
			{ "configure_indexer", WebApiMethod.ConfigureIndexer },
			{ "get_indexers", WebApiMethod.GetIndexers },
			{ "test_indexer", WebApiMethod.TestIndexer },
			{ "delete_indexer", WebApiMethod.DeleteIndexer },
			{ "get_sonarr_config", WebApiMethod.GetSonarrConfig },
			{ "apply_sonarr_config", WebApiMethod.ApplySonarrConfig },
			{ "test_sonarr", WebApiMethod.TestSonarr },
            { "get_jackett_config",WebApiMethod.GetJackettConfig},
            { "apply_jackett_config",WebApiMethod.ApplyJackettConfig},
            { "jackett_restart", WebApiMethod.JackettRestart },
		};

        IndexerManager indexerManager;
        SonarrApi sonarrApi;

        public WebApi(IndexerManager indexerManager, SonarrApi sonarrApi)
        {
            this.indexerManager = indexerManager;
            this.sonarrApi = sonarrApi;
        }

        public async Task<bool> HandleRequest(HttpListenerContext context)
        {
            string path = context.Request.Url.AbsolutePath.TrimStart('/');
            if (path == "")
                path = "index.html";

            var sysPath = Path.Combine(WebContentFolder, path.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (Array.IndexOf(StaticFiles, sysPath) > -1)
            {
                await ServeStaticFile(context, path);
                return true;
            }

            WebApi.WebApiMethod apiMethod;
            if (WebApi.WebApiMethods.TryGetValue(path, out apiMethod))
            {
                await ProcessWebApiRequest(context, apiMethod);
                return true;
            }

            return false;
        }

        async Task ServeStaticFile(HttpListenerContext context, string file)
        {
            var contentFile = File.ReadAllBytes(Path.Combine(WebContentFolder, file));
            context.Response.ContentType = MimeMapping.GetMimeMapping(file);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            try
            {
                await context.Response.OutputStream.WriteAsync(contentFile, 0, contentFile.Length);
            }
            catch (HttpListenerException)
            {
            }
        }

        async Task<JToken> ReadPostDataJson(Stream stream)
        {
            string postData = await new StreamReader(stream).ReadToEndAsync();
            return JObject.Parse(postData);
        }

        delegate Task<JToken> HandlerTask(HttpListenerContext context);

        async Task ProcessWebApiRequest(HttpListenerContext context, WebApiMethod method)
        {
            context.Response.ContentType = "text/json";
            context.Response.StatusCode = (int)HttpStatusCode.OK;

            HandlerTask handlerTask;

            switch (method)
            {
                case WebApiMethod.GetConfigForm:
                    handlerTask = HandleConfigForm;
                    break;
                case WebApiMethod.ConfigureIndexer:
                    handlerTask = HandleConfigureIndexer;
                    break;
                case WebApiMethod.GetIndexers:
                    handlerTask = HandleGetIndexers;
                    break;
                case WebApiMethod.TestIndexer:
                    handlerTask = HandleTestIndexer;
                    break;
                case WebApiMethod.DeleteIndexer:
                    handlerTask = HandleDeleteIndexer;
                    break;
                case WebApiMethod.GetSonarrConfig:
                    handlerTask = HandleGetSonarrConfig;
                    break;
                case WebApiMethod.ApplySonarrConfig:
                    handlerTask = HandleApplySonarrConfig;
                    break;
                case WebApiMethod.TestSonarr:
                    handlerTask = HandleTestSonarr;
                    break;
                case WebApiMethod.ApplyJackettConfig:
                    handlerTask = HandleApplyJackettConfig;
                    break;
                case WebApiMethod.GetJackettConfig:
                    handlerTask = HandleJackettConfig;
                    break;
                case WebApiMethod.JackettRestart:
                    handlerTask = HandleJackettRestart;
                    break;
                default:
                    handlerTask = HandleInvalidApiMethod;
                    break;
            }
            JToken jsonReply = await handlerTask(context);
            await ReplyWithJson(context, jsonReply, method.ToString());
        }

        async Task ReplyWithJson(HttpListenerContext context, JToken json, string apiCall)
        {
            try
            {
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json.ToString());
                await context.Response.OutputStream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing json to stream for API call " + apiCall + Environment.NewLine + ex.ToString());
            }
        }

        async Task<JToken> HandleTestSonarr(HttpListenerContext context)
        {
            JToken jsonReply = new JObject();
            try
            {
                await sonarrApi.TestConnection();
                jsonReply["result"] = "success";
            }
            catch (Exception ex)
            {
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;

                Program.LoggerInstance.Error(ex, "Error testing Sonarr");
            }
            return jsonReply;
        }

        async Task<JToken> HandleApplySonarrConfig(HttpListenerContext context)
        {
            JToken jsonReply = new JObject();
            try
            {
                var postData = await ReadPostDataJson(context.Request.InputStream);
                await sonarrApi.ApplyConfiguration(postData);
                jsonReply["result"] = "success";
            }
            catch (Exception ex)
            {
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;

                Program.LoggerInstance.Error(ex, "Error applying Sonarr config");
            }
            return jsonReply;
        }

        Task<JToken> HandleGetSonarrConfig(HttpListenerContext context)
        {
            JObject jsonReply = new JObject();
            try
            {
                jsonReply["config"] = sonarrApi.GetConfiguration().ToJson();
                jsonReply["result"] = "success";
            }
            catch (Exception ex)
            {
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;

                Program.LoggerInstance.Error(ex, "Error getting Sonarr config");
            }
            return Task.FromResult<JToken>(jsonReply);
        }

        Task<JToken> HandleInvalidApiMethod(HttpListenerContext context)
        {
            JToken jsonReply = new JObject();
            jsonReply["result"] = "error";
            jsonReply["error"] = "Invalid API method";
            return Task.FromResult<JToken>(jsonReply);
        }

        async Task<JToken> HandleConfigForm(HttpListenerContext context)
        {
            JToken jsonReply = new JObject();
            try
            {
                var postData = await ReadPostDataJson(context.Request.InputStream);
                string indexerString = (string)postData["indexer"];
                var indexer = indexerManager.GetIndexer(indexerString);
                var config = await indexer.GetConfigurationForSetup();
                jsonReply["config"] = config.ToJson();
                jsonReply["name"] = indexer.DisplayName;
                jsonReply["result"] = "success";
            }
            catch (Exception ex)
            {
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;

                Program.LoggerInstance.Error(ex, "Error handling config form");
            }
            return jsonReply;
        }

        async Task<JToken> HandleConfigureIndexer(HttpListenerContext context)
        {
            JToken jsonReply = new JObject();
            try
            {
                var postData = await ReadPostDataJson(context.Request.InputStream);
                string indexerString = (string)postData["indexer"];
                var indexer = indexerManager.GetIndexer(indexerString);
                jsonReply["name"] = indexer.DisplayName;
                await indexer.ApplyConfiguration(postData["config"]);
                await indexerManager.TestIndexer(indexer);
                jsonReply["result"] = "success";
            }
            catch (Exception ex)
            {
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;
                
                if (ex is ExceptionWithConfigData)
                {
                    jsonReply["config"] = ((ExceptionWithConfigData)ex).ConfigData.ToJson();
                }

                Program.LoggerInstance.Error(ex, "Error configuring indexer");
            }
            return jsonReply;
        }

        Task<JToken> HandleGetIndexers(HttpListenerContext context)
        {
            JToken jsonReply = new JObject();
            try
            {
                jsonReply["result"] = "success";
                jsonReply["api_key"] = ApiKey.CurrentKey;
                jsonReply["app_version"] = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                JArray items = new JArray();
                foreach (var i in indexerManager.Indexers.OrderBy(_=>_.Key))
                {
                    var indexer = i.Value;
                    var item = new JObject();
                    item["id"] = i.Key;
                    item["name"] = indexer.DisplayName;
                    item["description"] = indexer.DisplayDescription;
                    item["configured"] = indexer.IsConfigured;
                    item["site_link"] = indexer.SiteLink;
                    items.Add(item);
                }
                jsonReply["items"] = items;
            }
            catch (Exception ex)
            {
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;

                Program.LoggerInstance.Error(ex, "Error getting indexers");
            }
            return Task.FromResult<JToken>(jsonReply);
        }

        async Task<JToken> HandleTestIndexer(HttpListenerContext context)
        {
            JToken jsonReply = new JObject();
            try
            {
                var postData = await ReadPostDataJson(context.Request.InputStream);
                string indexerString = (string)postData["indexer"];
                var indexer = indexerManager.GetIndexer(indexerString);
                jsonReply["name"] = indexer.DisplayName;
                await indexerManager.TestIndexer(indexer);
                jsonReply["result"] = "success";
            }
            catch (Exception ex)
            {
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;

                Program.LoggerInstance.Error(ex, "Error testing indexer");
            }
            return jsonReply;
        }

        async Task<JToken> HandleDeleteIndexer(HttpListenerContext context)
        {
            JToken jsonReply = new JObject();
            try
            {
                var postData = await ReadPostDataJson(context.Request.InputStream);
                string indexerString = (string)postData["indexer"];
                indexerManager.DeleteIndexer(indexerString);
            }
            catch (Exception ex)
            {
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;

                Program.LoggerInstance.Error(ex, "Error deleting indexer");
            }
            return jsonReply;
        }


        //Jacket port functions
        Task<JToken> HandleJackettConfig(HttpListenerContext context)
        {
            JObject jsonReply = new JObject();
            try
            {
                jsonReply["config"] = server.ReadServerSettingsFile();
                jsonReply["result"] = "success";
            }
            catch (CustomException ex)
            {
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;

                Program.LoggerInstance.Error(ex, "Error configuring Jackett");
            }
            catch (Exception ex)
            {
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;

                Program.LoggerInstance.Error(ex, "Error configuring Jackett");
            }
            return Task.FromResult<JToken>(jsonReply);
        }

        async Task<JToken> HandleApplyJackettConfig(HttpListenerContext context)
        {
            JToken jsonReply = new JObject();

            try
            {
                var postData = await ReadPostDataJson(context.Request.InputStream);
                int port = await server.ApplyPortConfiguration(postData);
                jsonReply["result"] = "success";
                jsonReply["port"] = port;
            }
            catch (Exception ex)
            {
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;

                Program.LoggerInstance.Error(ex, "Error applying Jackett config");
            }
            return jsonReply;
        }

        async Task<JToken> HandleJackettRestart(HttpListenerContext context)
        {
            Program.RestartServer();
            return null;
        }


    }
}
