﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace YALV.Core.Plugins
{
    public class PluginManager
    {
        private static PluginManager _instance;
        private static readonly Type iYalvPluginType = typeof(IYalvPlugin);
        private IPluginContext _pluginContext = new PluginContext();
        private readonly List<IYalvPlugin> _plugins = new List<IYalvPlugin>();

        private PluginManager()
        {
            Dictionary<Type, object> potentialParams = new Dictionary<Type, object>();
            potentialParams.Add(typeof(IPluginContext), _pluginContext);

            SearchAssembly(this.GetType().Assembly, potentialParams);

            DirectoryInfo runningDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins"));
            foreach (FileInfo file in runningDir.GetFiles("*.dll"))
            {
                Assembly assembly = Assembly.LoadFile(file.FullName);
                SearchAssembly(assembly, potentialParams);
            }

            _plugins.Sort((a, b) => (a.Priority - b.Priority));
        }

        public IPluginContext Context { get { return _pluginContext; } }

        private void SearchAssembly(Assembly assembly, Dictionary<Type, object> potentialParams)
        {
            foreach (Module module in assembly.Modules)
            {
                foreach (Type t in module.GetTypes())
                {
                    if (t.IsClass && !t.IsAbstract && iYalvPluginType.IsAssignableFrom(t))
                    {
                        _plugins.Add(Create(t, potentialParams));
                    }
                }
            }
        }

        private IYalvPlugin Create(Type type, Dictionary<Type, object> potentialParams)
        {
            ConstructorInfo[] ctors = type.GetConstructors();
            object[] parameters = GetParams(ctors, potentialParams);
            if (parameters != null)
            {
                return (IYalvPlugin)Activator.CreateInstance(type, parameters);
            }

            throw new InvalidOperationException(string.Format("No suiting counstructor found for type {0}",  type.Name));
        }

        private object[] GetParams(ConstructorInfo[] ctors, Dictionary<Type, object> potentialParams)
        {
            foreach(ConstructorInfo ctor in ctors)
            {
                object[] args = GetParams(ctor, potentialParams);
                if (args != null)
                {
                    return args;
                }
            }
            return null;
        }
        private object[] GetParams(ConstructorInfo ctor, Dictionary<Type, object> potentialParams)
        {
            List<object> result = new List<object>();
            foreach(ParameterInfo p in ctor.GetParameters())
            {
                object o;
                if (!TryGetParam(potentialParams, p.ParameterType, out o))
                {
                    return null;
                }
                result.Add(o);
            }
            return result.ToArray();
        }

        private bool TryGetParam(Dictionary<Type, object> potentialParams, Type paramType, out object obj)
        {
            foreach(KeyValuePair<Type, object> kv in potentialParams)
            {
                if (paramType.IsAssignableFrom(kv.Key))
                {
                    obj = kv.Value;
                    return true;
                }
            }

            obj = null;
            return false;
        }

        public IReadOnlyList<T> GetPlugins<T>() where T: IYalvPlugin
        {
            return _plugins.OfType<T>().ToList();
        }

        public static PluginManager Instance { get
            {
                if (_instance == null){
                    _instance = new PluginManager();
                }
                return _instance;
            }
        }
    }
}
