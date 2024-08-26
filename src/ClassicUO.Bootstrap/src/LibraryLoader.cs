using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace ClassicUO
{
    public static class Native
    {
        private static readonly NativeLoader _loader;

        static Native()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                _loader = new WinNativeLoader();
            }
            else
            {
                _loader = new UnixNativeLoader();
            }
            PrintSystemInfo();
        }

        private static void Log(string message)
        {
            Debug.WriteLine($"[Native Loader] {message}");
            Console.WriteLine($"[Native Loader] {message}");
        }

        public static void PrintSystemInfo()
        {
            Log($"OS Description: {RuntimeInformation.OSDescription}");
            Log($"OS Architecture: {RuntimeInformation.OSArchitecture}");
            Log($"Framework Description: {RuntimeInformation.FrameworkDescription}");
            Log($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
        }

        public static IntPtr LoadLibrary(string name)
        {
            try
            {
                Log($"Attempting to load library: {name}");
                IntPtr handle = _loader.LoadLibrary(name);
                Log($"Successfully loaded library: {name}, Handle: {handle}");
                return handle;
            }
            catch (Exception ex)
            {
                Log($"Exception in LoadLibrary: {ex.Message}");
                Log($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public static IntPtr GetProcessAddress(IntPtr module, string name)
        {
            try
            {
                Log($"Getting process address for: {name} in module: {module}");
                IntPtr address = _loader.GetProcessAddress(module, name);
                if (address != IntPtr.Zero)
                {
                    Log($"Successfully got process address for: {name}, Address: {address}");
                }
                else
                {
                    Log($"Failed to get process address for: {name}");
                }
                return address;
            }
            catch (Exception ex)
            {
                Log($"Exception in GetProcessAddress: {ex.Message}");
                Log($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public static int FreeLibrary(IntPtr module)
        {
            try
            {
                Log($"Freeing library: {module}");
                int result = _loader.FreeLibrary(module);
                if (result == 0)
                {
                    Log($"Successfully freed library: {module}");
                }
                else
                {
                    Log($"Failed to free library: {module}");
                }
                return result;
            }
            catch (Exception ex)
            {
                Log($"Exception in FreeLibrary: {ex.Message}");
                Log($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        abstract class NativeLoader
        {
            public abstract IntPtr LoadLibrary(string name);
            public abstract IntPtr GetProcessAddress(IntPtr module, string name);
            public abstract int FreeLibrary(IntPtr module);
        }

        private class WinNativeLoader : NativeLoader
        {
            private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;
            
            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            private static extern IntPtr LoadLibraryExW([MarshalAs(UnmanagedType.LPWStr)] string lpLibFileName, IntPtr hFile, uint dwFlags);

            [DllImport("kernel32", EntryPoint = "GetProcAddress", CharSet = CharSet.Ansi)]
            private static extern IntPtr GetProcAddress_WIN(IntPtr module, [MarshalAs(UnmanagedType.LPStr)] string procName);

            [DllImport("kernel32", EntryPoint = "FreeLibrary")]
            private static extern int FreeLibrary_WIN(IntPtr module);

            public override IntPtr LoadLibrary(string name)
            {
                return LoadLibraryExW(name, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
            }

            public override IntPtr GetProcessAddress(IntPtr module, string name)
            {
                return GetProcAddress_WIN(module, name);
            }

            public override int FreeLibrary(IntPtr module)
            {
                return FreeLibrary_WIN(module);
            }
        }

        private class UnixNativeLoader : NativeLoader
        {
            public const int RTLD_NOW = 0x002;

            private static class Libdl1
            {
                private const string LibName = "libdl";

                [DllImport(LibName)]
                public static extern IntPtr dlopen(string fileName, int flags);

                [DllImport(LibName)]
                public static extern IntPtr dlsym(IntPtr handle, string name);

                [DllImport(LibName)]
                public static extern int dlclose(IntPtr handle);

                [DllImport(LibName)]
                public static extern IntPtr dlerror();
            }

            private static class Libdl2
            {
                private const string LibName = "libdl.so.2";

                [DllImport(LibName)]
                public static extern IntPtr dlopen(string fileName, int flags);

                [DllImport(LibName)]
                public static extern IntPtr dlsym(IntPtr handle, string name);

                [DllImport(LibName)]
                public static extern int dlclose(IntPtr handle);

                [DllImport(LibName)]
                public static extern IntPtr dlerror();
            }

            private static bool m_useLibdl1;

            static UnixNativeLoader()
            {
                try
                {
                    Libdl1.dlerror();
                    m_useLibdl1 = true;
                    Log("Using libdl1 for dynamic loading");
                }
                catch
                {
                    m_useLibdl1 = false;
                    Log("Using libdl2 for dynamic loading");
                }
            }

            public override IntPtr LoadLibrary(string name)
            {
                Log($"Attempting to load library: {name}");
                var handle = m_useLibdl1 ? Libdl1.dlopen(name, RTLD_NOW) : Libdl2.dlopen(name, RTLD_NOW);
                if (handle == IntPtr.Zero)
                {
                    var error = m_useLibdl1 ? Libdl1.dlerror() : Libdl2.dlerror();
                    Log($"Failed to load library: {name}, Error: {Marshal.PtrToStringAnsi(error)}");
                    throw new Exception($"Failed to load library: {name}, Error: {Marshal.PtrToStringAnsi(error)}");
                }
                Log($"Successfully loaded library: {name}, Handle: {handle}");
                return handle;
            }

            public override IntPtr GetProcessAddress(IntPtr module, string name)
            {
                Log($"Getting process address for: {name} in module: {module}");
                var address = m_useLibdl1 ? Libdl1.dlsym(module, name) : Libdl2.dlsym(module, name);
                if (address == IntPtr.Zero)
                {
                    var error = m_useLibdl1 ? Libdl1.dlerror() : Libdl2.dlerror();
                    Log($"Failed to get process address for: {name}, Error: {Marshal.PtrToStringAnsi(error)}");
                }
                else
                {
                    Log($"Successfully got process address for: {name}, Address: {address}");
                }
                return address;
            }

            public override int FreeLibrary(IntPtr module)
            {
                Log($"Freeing library: {module}");
                var result = m_useLibdl1 ? Libdl1.dlclose(module) : Libdl2.dlclose(module);
                if (result != 0)
                {
                    var error = m_useLibdl1 ? Libdl1.dlerror() : Libdl2.dlerror();
                    Log($"Failed to free library: {module}, Error: {Marshal.PtrToStringAnsi(error)}");
                }
                else
                {
                    Log($"Successfully freed library: {module}");
                }
                return result;
            }
        }
    }
}