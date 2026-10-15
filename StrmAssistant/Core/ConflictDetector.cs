using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;

namespace StrmAssistant.Core
{
    /// <summary>
    /// 冲突插件检测：Emby 为每个插件使用独立的 <see cref="AssemblyLoadContext"/>。
    /// 若在「非默认且非本插件自身」的加载上下文中发现同名程序集
    /// （<c>StrmAssistant</c> / <c>StrmAssistantLite</c>），说明社区版/Lite 与本插件同时安装，
    /// 二者会重复打补丁导致行为异常，应予提示。
    /// </summary>
    public static class ConflictDetector
    {
        private static readonly string[] ConflictNames = { "StrmAssistant", "StrmAssistantLite" };

        public static string[] GetConflictingAssemblies()
        {
            try
            {
                var ownAssembly = typeof(Plugin).Assembly;
                var ownName = ownAssembly.GetName().Name;
                var ownContext = AssemblyLoadContext.GetLoadContext(ownAssembly);

                var found = new List<string>();

                foreach (var context in AssemblyLoadContext.All)
                {
                    if (context == AssemblyLoadContext.Default || context == ownContext) continue;

                    IEnumerable<System.Reflection.Assembly> assemblies;
                    try
                    {
                        assemblies = context.Assemblies;
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var assembly in assemblies)
                    {
                        var name = assembly.GetName().Name;
                        if (string.IsNullOrEmpty(name)) continue;

                        if (ConflictNames.Contains(name, StringComparer.OrdinalIgnoreCase) ||
                            string.Equals(name, ownName, StringComparison.OrdinalIgnoreCase))
                        {
                            found.Add(name);
                        }
                    }
                }

                return found.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public static bool HasConflictPluginLoaded() => GetConflictingAssemblies().Length > 0;
    }
}
