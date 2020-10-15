﻿/*
 * Copyright 2020 VMware, Inc.
 * SPDX-License-Identifier: EPL-2.0
 */

using SingletonClient.Implementation.Helpers;
using SingletonClient.Implementation.Support;
using System;
using System.Collections;
using System.Reflection;
using YamlDotNet.RepresentationModel;

namespace SingletonClient.Implementation
{
    public interface ISingletonClientManager
    {
        IConfig LoadConfig(string resourceBaseName, Assembly assembly, string configResourceName);
        IConfig GetConfig(string product, string version);
        IRelease GetRelease(IConfig config);
        ICacheManager GetCacheManager(string cacheManagerName);
        ILog GetLogger(string loggerName);
        IResourceParser GetResourceParser(string parserName);
        IAccessService GetAccessService(string accessServiceName);
        void SetFallbackConfig(string configText);
        string GetFallbackLocale(string locale);
    }

    public class SingletonClientManager : ISingletonClientManager, IExtension
    {
        private static SingletonClientManager _instance = new SingletonClientManager();

        public static SingletonClientManager GetInstance()
        {
            return _instance;
        }

        // key: (string) product name  
        // value: (Hashtable) versions -> 
        //     key: (string) version
        //     value: (ISingletonRelease) release object
        private Hashtable _products = SingletonUtil.NewHashtable();

        // key: (string) type name of cache manager
        // value: (ICacheManager) cache manager object
        private Hashtable _cacheManagers = SingletonUtil.NewHashtable();

        // key: (string) type name of logger
        // value: (ILog) logger object
        private Hashtable _loggers = SingletonUtil.NewHashtable();

        // key: (string) type name of parser
        // value: (ISourceParser) parser object
        private Hashtable _parsers = SingletonUtil.NewHashtable();

        // key: (string) type name of accessing service
        // value: (IAccessService) accessing service object
        private Hashtable _accessServices = SingletonUtil.NewHashtable();

        private Hashtable _localeNameMap = SingletonUtil.NewHashtable();

        private SingletonClientManager()
        {
            ICacheManager cacheManager = new SingletonCacheManager();
            RegisterCacheManager(cacheManager, ConfigConst.DefaultType);

            ILog logger = new SingletonLogger();
            RegisterLogger(logger, ConfigConst.DefaultType);

            IResourceParser parser = new SingletonParserProperties();
            RegisterResourceParser(parser, ConfigConst.FormatProperties);

            IResourceParser bundleParser = new SingletonParserBundle();
            RegisterResourceParser(bundleParser, ConfigConst.FormatBundle);

            IAccessService accessService = new SingletonAccessService();
            RegisterAccessService(accessService, ConfigConst.DefaultType);

            LoadFallbackDefine();
        }

        private void LoadFallbackDefine()
        {
            string nameSpace = System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Namespace;
            string resName = nameSpace + ".SingletonRes.Singleton";
            Assembly assembly = typeof(SingletonClientManager).Assembly;

            byte[] bytes = SingletonUtil.ReadResource(resName, assembly, "fallback");
            string configText = SingletonUtil.ConvertToText(bytes);

            SetFallbackConfig(configText);
        }

        public void SetFallbackConfig(string configText)
        {
            YamlMappingNode configRoot = SingletonUtil.GetYamlRoot(configText);
            var valuesMapping = (YamlMappingNode)configRoot.Children[new YamlScalarNode("locale")];
            foreach (var tuple in valuesMapping.Children)
            {
                _localeNameMap.Add(tuple.Key.ToString(), tuple.Value.ToString());
            }
        }

        public string GetFallbackLocale(string locale)
        {
            locale = CultureHelper.GetCulture(locale).Name;

            string fallback = (string)_localeNameMap[locale];
            if (fallback != null)
            {
                return fallback;
            }
            string[] parts = locale.Split('-');
            if (parts.Length == 1)
            {
                return locale;
            }

            return parts[0];
        }

        private Hashtable GetProductVersions(string product, bool add)
        {
            Hashtable versions = (Hashtable)_products[product];
            if (versions == null && add)
            {
                _products[product] = SingletonUtil.NewHashtable();
                versions = (Hashtable)_products[product];
            }
            return versions;
        }

        private ISingletonRelease GetRelease(
            Hashtable versions, string version, bool add)
        {
            if(versions == null || version == null)
            {
                return null;
            }
            ISingletonRelease releaseObject = (ISingletonRelease)versions[version];
            if (releaseObject == null && add)
            {
                releaseObject = new SingletonRelease();
                versions[version] = releaseObject;
            }
            return releaseObject;
        }

        public IConfig LoadConfig(
            string resourceBaseName, Assembly assembly, string configResourceName)
        {
            SingletonConfig config = new SingletonConfig(resourceBaseName, assembly);
            string text = config.ReadResourceText(configResourceName);
            config.SetConfigData(text);

            ISingletonConfig singletonConfig = new SingletonConfigWrapper(config);
            string product = singletonConfig.GetProduct();
            string version = singletonConfig.GetVersion();

            Hashtable versions = GetProductVersions(product, true);
            ISingletonRelease releaseObject = GetRelease(versions, version, true);
            if (releaseObject.GetRelease().GetConfig() == null)
            {
                releaseObject.SetConfig(config);
            }
            return releaseObject.GetRelease().GetConfig();
        }

        public IConfig GetConfig(string product, string version)
        {
            ISingletonRelease releaseObject = GetRelease(product, version);
            return (releaseObject == null) ? null : releaseObject.GetRelease().GetConfig();
        }

        public IRelease GetRelease(IConfig config)
        {
            if (config == null)
            {
                return null;
            }

            ISingletonConfig wrapper = new SingletonConfigWrapper(config);
            ISingletonRelease releaseObject = GetRelease(wrapper.GetProduct(), wrapper.GetVersion());
            return releaseObject.GetRelease();
        }

        public void RegisterLogger(ILog logger, string loggerName)
        {
            _loggers[loggerName] = logger;
        }

        private object GetItemFromTable(Hashtable table, string name)
        {
            object found = string.IsNullOrEmpty(name) ? null : table[name];
            if (found == null)
            {
                found = table[ConfigConst.DefaultType];
            }
            return found;
        }

        public ICacheManager GetCacheManager(string cacheManagerName)
        {
            return (ICacheManager)GetItemFromTable(_cacheManagers, cacheManagerName);
        }

        public ILog GetLogger(string loggerName)
        {
            return (ILog)GetItemFromTable(_loggers, loggerName);
        }

        public IResourceParser GetResourceParser(string parserName)
        {
            return (IResourceParser)GetItemFromTable(_parsers, parserName);
        }

        public IAccessService GetAccessService(string accessServiceName)
        {
            return (IAccessService)GetItemFromTable(_accessServices, accessServiceName);
        }

        private ISingletonRelease GetRelease(string product, string version)
        {
            Hashtable versions = GetProductVersions(product, false);
            if (versions == null)
            {
                return null;
            }
            ISingletonRelease rel = GetRelease(versions, version, false);
            return rel;
        }

        public void RegisterCacheManager(ICacheManager cacheManager, string cacheManagerName)
        {
            _cacheManagers[cacheManagerName] = cacheManager;
        }

        public void RegisterAccessService(IAccessService accessService, string accessrName)
        {
            _accessServices[accessrName] = accessService;
        }

        public void RegisterResourceParser(IResourceParser parser, string parserName)
        {
            _parsers[parserName] = parser;
        }
    }
}