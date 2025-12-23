using System;
using System.IO;
using System.Reflection;
using NLua;

namespace Voidstrap
{
    public static class LuaScriptManager
    {
        private const string LOG_IDENT = "LuaScriptManager";
        private static Lua? _currentLua = null;
        
        public static void ExecuteScript()
        {
            try
            {
                // Check if Lua scripting is enabled
                if (!App.Settings.Prop.EnableLuaScripting)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Lua scripting is disabled");
                    return;
                }

                string luaScriptPath = Path.Combine(Paths.Base, "autoexecute.lua");
                
                // Check if the script file exists
                if (!File.Exists(luaScriptPath))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Lua script not found at {luaScriptPath}");
                    return;
                }

                // Read the Lua script
                string luaScript = File.ReadAllText(luaScriptPath);
                
                if (string.IsNullOrWhiteSpace(luaScript))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Lua script is empty");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT, "Executing Lua script...");

                using (var lua = new Lua())
                {
                    // Store the Lua instance for use in loadfunc
                    _currentLua = lua;
                    
                    // TODO: Register custom functions to the Lua environment here
                    // Example: lua.RegisterFunction("customFunction", null, typeof(LuaScriptManager).GetMethod("CustomFunction"));
                    
                    // Register the example function
                    lua.RegisterFunction("ExampleFunction", null, typeof(LuaScriptManager).GetMethod("ExampleFunction"));
                    
                    // Register the DLL loading function
                    lua.RegisterFunction("load", null, typeof(LuaScriptManager).GetMethod("LoadDll"));
                    
                    // Register the function loading function
                    lua.RegisterFunction("loadfunc", null, typeof(LuaScriptManager).GetMethod("LoadFunction"));
                    
                    // Register the WaitForRoblox function
                    lua.RegisterFunction("WaitForRoblox", null, typeof(LuaScriptManager).GetMethod("WaitForRoblox"));
                    
                    // Execute the Lua script
                    lua.DoString(luaScript);
                    
                    _currentLua = null;
                }

                App.Logger.WriteLine(LOG_IDENT, "Lua script executed successfully");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error executing Lua script: {ex.Message}");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        /// <summary>
        /// Waits for Roblox to launch and become visible, then executes the Lua script
        /// </summary>
        public static void WaitForRobloxAndExecuteScript()
        {
            try
            {
                App.Logger.WriteLine(LOG_IDENT, "Waiting for Roblox to launch before executing Lua script...");

                string processName = App.RobloxPlayerAppName.Split('.')[0];
                var sw = System.Diagnostics.Stopwatch.StartNew();

                while (sw.Elapsed.TotalSeconds < 60)
                {
                    System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName(processName);

                    if (processes.Length > 0)
                    {
                        System.Diagnostics.Process roblox = processes[0];
                        roblox.Refresh();

                        if (roblox.MainWindowHandle != IntPtr.Zero)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Roblox window found, executing Lua script");
                            
                            // Wait a bit more to ensure Roblox is fully loaded
                            System.Threading.Thread.Sleep(1000);
                            
                            // Execute the Lua script
                            ExecuteScript();
                            return;
                        }
                    }

                    System.Threading.Thread.Sleep(200);
                }

                App.Logger.WriteLine(LOG_IDENT, "Timed out waiting for Roblox window");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error in WaitForRobloxAndExecuteScript: {ex.Message}");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        /// <summary>
        /// Example function that can be called from Lua
        /// Usage in Lua: result = ExampleFunction()
        /// </summary>
        public static bool ExampleFunction()
        {
            App.Logger.WriteLine(LOG_IDENT, "ExampleFunction called from Lua!");
            return true;
        }

        /// <summary>
        /// Loads a DLL from the Voidstrap directory and executes its Main function
        /// Usage in Lua: load("MyDll.dll") or load("MyDll")
        /// The DLL must have a public static class with a public static void Main() method
        /// </summary>
        public static void LoadDll(string dllName)
        {
            try
            {
                // Add .dll extension if not present
                if (!dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    dllName += ".dll";
                }

                string dllPath = Path.Combine(Paths.Base, dllName);
                
                if (!File.Exists(dllPath))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"DLL not found: {dllPath}");
                    throw new FileNotFoundException($"DLL not found: {dllName}");
                }

                App.Logger.WriteLine(LOG_IDENT, $"Loading DLL: {dllPath}");

                // Load the assembly
                Assembly assembly = Assembly.LoadFrom(dllPath);
                
                // Find a type with a Main method
                Type? mainType = null;
                MethodInfo? mainMethod = null;

                foreach (Type type in assembly.GetTypes())
                {
                    // Look for a public static Main method
                    mainMethod = type.GetMethod("Main", BindingFlags.Public | BindingFlags.Static);
                    
                    if (mainMethod != null)
                    {
                        mainType = type;
                        break;
                    }
                }

                if (mainMethod == null)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"No public static Main method found in {dllName}");
                    throw new InvalidOperationException($"No public static Main method found in {dllName}");
                }

                App.Logger.WriteLine(LOG_IDENT, $"Executing Main method from {mainType?.Name} in {dllName}");

                // Invoke the Main method
                mainMethod.Invoke(null, mainMethod.GetParameters().Length == 0 ? null : new object[] { new string[0] });

                App.Logger.WriteLine(LOG_IDENT, $"Successfully executed Main from {dllName}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error loading DLL '{dllName}': {ex.Message}");
                App.Logger.WriteException(LOG_IDENT, ex);
                throw;
            }
        }

        /// <summary>
        /// Loads a specific function from a DLL and registers it in the Lua environment
        /// Usage in Lua: loadfunc("MyDll.dll", "MyClass", "MyMethod", "myFunc")
        /// After calling, you can use: myFunc(args...)
        /// </summary>
        /// <param name="dllName">Name of the DLL file (with or without .dll extension)</param>
        /// <param name="className">Full name of the class containing the method</param>
        /// <param name="methodName">Name of the method to load</param>
        /// <param name="luaFunctionName">Name to register the function as in Lua</param>
        public static void LoadFunction(string dllName, string className, string methodName, string luaFunctionName)
        {
            try
            {
                if (_currentLua == null)
                {
                    throw new InvalidOperationException("Lua environment is not initialized");
                }

                // Add .dll extension if not present
                if (!dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    dllName += ".dll";
                }

                string dllPath = Path.Combine(Paths.Base, dllName);
                
                if (!File.Exists(dllPath))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"DLL not found: {dllPath}");
                    throw new FileNotFoundException($"DLL not found: {dllName}");
                }

                App.Logger.WriteLine(LOG_IDENT, $"Loading function {className}.{methodName} from {dllPath}");

                // Load the assembly
                Assembly assembly = Assembly.LoadFrom(dllPath);
                
                // Find the type
                Type? targetType = assembly.GetType(className);
                
                if (targetType == null)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Class '{className}' not found in {dllName}");
                    throw new InvalidOperationException($"Class '{className}' not found in {dllName}");
                }

                // Find the method
                MethodInfo? method = targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                
                if (method == null)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Public static method '{methodName}' not found in class '{className}'");
                    throw new InvalidOperationException($"Public static method '{methodName}' not found in class '{className}'");
                }

                App.Logger.WriteLine(LOG_IDENT, $"Registering function as '{luaFunctionName}' in Lua environment");

                // Register the function in Lua
                _currentLua.RegisterFunction(luaFunctionName, null, method);

                App.Logger.WriteLine(LOG_IDENT, $"Successfully registered '{luaFunctionName}' from {className}.{methodName}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error loading function: {ex.Message}");
                App.Logger.WriteException(LOG_IDENT, ex);
                throw;
            }
        }

        /// <summary>
        /// Waits for Roblox to launch and become visible, then executes a Lua callback function
        /// Usage in Lua: WaitForRoblox(function() print("Roblox is ready!") end)
        /// </summary>
        /// <param name="callback">Lua function to execute when Roblox is ready</param>
        public static void WaitForRoblox(NLua.LuaFunction callback)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    App.Logger.WriteLine(LOG_IDENT, "WaitForRoblox: Starting wait for Roblox process");

                    string processName = App.RobloxPlayerAppName.Split('.')[0];
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    while (sw.Elapsed.TotalSeconds < 60)
                    {
                        System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName(processName);

                        if (processes.Length > 0)
                        {
                            System.Diagnostics.Process roblox = processes[0];
                            roblox.Refresh();

                            if (roblox.MainWindowHandle != IntPtr.Zero)
                            {
                                App.Logger.WriteLine(LOG_IDENT, "WaitForRoblox: Roblox window found, executing callback");

                                try
                                {
                                    // Execute the Lua callback function
                                    callback.Call();
                                    App.Logger.WriteLine(LOG_IDENT, "WaitForRoblox: Callback executed successfully");
                                }
                                catch (Exception ex)
                                {
                                    App.Logger.WriteLine(LOG_IDENT, $"WaitForRoblox: Error executing callback: {ex.Message}");
                                    App.Logger.WriteException(LOG_IDENT, ex);
                                }

                                return;
                            }
                        }

                        System.Threading.Thread.Sleep(500);
                    }

                    App.Logger.WriteLine(LOG_IDENT, "WaitForRoblox: Timed out waiting for Roblox window");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"WaitForRoblox: Error: {ex.Message}");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            });
        }
    }
}
