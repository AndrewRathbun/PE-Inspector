﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;

namespace PECoff
{
    public static class Extensions
    {
        public struct Int64Words
        {
            public ushort Word0;
            public ushort Word1;
            public ushort Word2;
            public ushort Word3;
        }

        public struct UInt32Words
        {
            public ushort LOW;
            public ushort HI;
        }

        public static int WordCount(this String str)
        {
            return str.Split(new char[] { ' ', '.', '?' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public static UInt16 ReverseBytes(this UInt16 value)
        {
            return (UInt16)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }

        public static UInt32 ReverseBytes(this UInt32 value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 | 
                   (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;
        }

        public static Int32 ReverseBytes(this Int32 value)
        {
            UInt32 x = Convert.ToUInt32(value);

            return Convert.ToInt32((x & 0x000000FF) << 24 | (x & 0x0000FF00) << 8 | (x & 0x00FF0000) >> 8 | (x & 0xFF000000) >> 24);
        }

        public static UInt64 ReverseBytes(this UInt64 value)
        {
            return (value & 0x00000000000000FFUL) << 56 | (value & 0x000000000000FF00UL) << 40 |
                   (value & 0x0000000000FF0000UL) << 24 | (value & 0x00000000FF000000UL) << 8 |
                   (value & 0x000000FF00000000UL) >> 8 | (value & 0x0000FF0000000000UL) >> 24 |
                   (value & 0x00FF000000000000UL) >> 40 | (value & 0xFF00000000000000UL) >> 56;
        }

        public static T ToStructure<T>(this byte[] bytes) where T : struct
        {
            // Thanks to coincoin @ http://stackoverflow.com/questions/2871/reading-a-c-c-data-structure-in-c-sharp-from-a-byte-array
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T retVal = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();

            return retVal;
        }

        public static T ToStructure<T>(this IntPtr value) where T : struct
        {
            T retVal = (T)Marshal.PtrToStructure(value, typeof(T));
            return retVal;
        }

        public static Int64Words GetWords(this Int64 value)
        {
            byte[] b = BitConverter.GetBytes(value);
            return b.ToStructure<Int64Words>();
        }

        public static UInt32Words GetWords(this UInt32 value)
        {
            byte[] b = BitConverter.GetBytes(value);
            return b.ToStructure<UInt32Words>();
        }
    }


    public class Search
    {
        /**
       * Returns the index within this string of the first occurrence of the
       * specified substring. If it is not a substring, return -1.
       * 
       * @param haystack The string to be scanned
       * @param needle The target string to search
       * @return The start index of the substring
       */
        public static long IndexOf(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0)
            {
                return 0;
            }

            long[] charTable = MakeCharTable(needle);
            long[] offsetTable = MakeOffsetTable(needle);
            for (long i = needle.Length - 1, j; i < haystack.Length; )
            {
                for (j = needle.Length - 1; needle[j] == haystack[i]; --i, --j)
                {
                    if (j == 0)
                    {
                        return i;
                    }
                }
                // i += needle.length - j; // For naive method
                i += Math.Max(offsetTable[needle.Length - 1 - j], charTable[haystack[i]]);
            }
            return -1;
        }

        /**
         * Makes the jump table based on the mismatched character information.
         */
        private static long[] MakeCharTable(byte[] needle)
        {
            int ALPHABET_SIZE = 256;
            long[] table = new long[ALPHABET_SIZE];
            for (long i = 0; i < table.Length; ++i)
            {
                table[i] = needle.Length;
            }
            for (long i = 0; i < needle.Length - 1; ++i)
            {
                table[needle[i]] = needle.Length - 1 - i;
            }
            return table;
        }

        /**
         * Makes the jump table based on the scan offset which mismatch occurs.
         */
        private static long[] MakeOffsetTable(byte[] needle)
        {
            long[] table = new long[needle.Length];
            long lastPrefixPosition = needle.Length;
            for (long i = needle.Length - 1; i >= 0; --i)
            {
                if (IsPrefix(needle, i + 1))
                {
                    lastPrefixPosition = i + 1;
                }
                table[needle.Length - 1 - i] = lastPrefixPosition - i + needle.Length - 1;
            }
            for (long i = 0; i < needle.Length - 1; ++i)
            {
                long slen = SuffixLength(needle, i);
                table[slen] = needle.Length - 1 - i + slen;
            }
            return table;
        }

        /**
         * Is needle[p:end] a prefix of needle?
         */
        private static bool IsPrefix(byte[] needle, long p)
        {
            for (long i = p, j = 0; i < needle.Length; ++i, ++j)
            {
                if (needle[i] != needle[j])
                {
                    return false;
                }
            }
            return true;
        }

        /**
         * Returns the maximum length of the substring ends at p and is a suffix.
         */
        private static long SuffixLength(byte[] needle, long p)
        {
            long len = 0;
            for (long i = p, j = needle.Length - 1;
                 i >= 0 && needle[i] == needle[j]; --i, --j)
            {
                len += 1;
            }
            return len;
        }
    
    
    }

    public class FileVersionInfo
    {
        private MemoryStream _ms;
        private VS_VERSIONINFO vi;

        #region enums
        [Flags]
        private enum FileFlags : uint
        {
            VS_FF_DEBUG = 0x00000001,
            VS_FF_INFOINFERRED = 0x00000010,
            VS_FF_PATCHED = 0x00000004,
            VS_FF_PRERELEASE = 0x00000002,
            VS_FF_PRIVATEBUILD = 0x00000008,
            VS_FF_SPECIALBUILD = 0x00000020
        }

        private enum FileOS : uint
        {
            VOS_DOS = 0x00010000, // The file was designed for MS-DOS.
            VOS_NT = 0x00040000, // The file was designed for Windows NT.
            VOS__WINDOWS16 = 0x00000001, // The file was designed for 16-bit Windows.
            VOS__WINDOWS32 = 0x00000004, // The file was designed for 32-bit Windows.
            VOS_OS216 = 0x00020000, // The file was designed for 16-bit OS/2.
            VOS_OS232 = 0x00030000, // The file was designed for 32-bit OS/2.
            VOS__PM16 = 0x00000002, // The file was designed for 16-bit Presentation Manager.
            VOS__PM32 = 0x00000003, // The file was designed for 32-bit Presentation Manager.
            VOS_UNKNOWN = 0x00000000, // The operating system for which the file was designed is unknown to the system.

            VOS_DOS_WINDOWS16 = 0x00010001, // The file was designed for 16-bit Windows running on MS-DOS.
            VOS_DOS_WINDOWS32 = 0x00010004, // The file was designed for 32-bit Windows running on MS-DOS.
            VOS_NT_WINDOWS32 = 0x00040004, // The file was designed for Windows NT.
            VOS_OS216_PM16 = 0x00020002, // The file was designed for 16-bit Presentation Manager running on 16-bit OS/2.
            VOS_OS232_PM32 = 0x00030003 // The file was designed for 32-bit Presentation Manager running on 32-bit OS/2.
        }

        private enum FileType : uint
        {
            VFT_APP = 0x00000001, // The file contains an application.
            VFT_DLL = 0x00000002, // The file contains a DLL.
            VFT_DRV = 0x00000003, // The file contains a device driver. If dwFileType is VFT_DRV, dwFileSubtype contains a more specific description of the driver.
            VFT_FONT = 0x00000004, // The file contains a font. If dwFileType is VFT_FONT, dwFileSubtype contains a more specific description of the font file.
            VFT_STATIC_LIB = 0x00000007, // The file contains a static-link library.
            VFT_UNKNOWN = 0x00000000, // The file type is unknown to the system.
            VFT_VXD = 0x00000005 // The file contains a virtual device.
        }

        private enum FileDRVSubtype : uint
        {

            VFT2_DRV_COMM = 0x0000000A, // The file contains a communications driver.
            VFT2_DRV_DISPLAY = 0x00000004, // The file contains a display driver.
            VFT2_DRV_INSTALLABLE = 0x00000008, // The file contains an installable driver.
            VFT2_DRV_KEYBOARD = 0x00000002, // The file contains a keyboard driver.
            VFT2_DRV_LANGUAGE = 0x00000003, // The file contains a language driver.
            VFT2_DRV_MOUSE = 0x00000005, // The file contains a mouse driver.
            VFT2_DRV_NETWORK = 0x00000006, // The file contains a network driver.
            VFT2_DRV_PRINTER = 0x00000001, // The file contains a printer driver.
            VFT2_DRV_SOUND = 0x00000009, // The file contains a sound driver.
            VFT2_DRV_SYSTEM = 0x00000007, // The file contains a system driver.
            VFT2_DRV_VERSIONED_PRINTER = 0x0000000C, // The file contains a versioned printer driver.
            VFT2_UNKNOWN = 0x00000000 // The driver type is unknown by the system.
        }
        private enum FileFNTSubtype : uint
        {
            VFT2_FONT_RASTER = 0x00000001, // The file contains a raster font.
            VFT2_FONT_TRUETYPE = 0x00000003, // The file contains a TrueType font.
            VFT2_FONT_VECTOR = 0x00000002, // The file contains a vector font.
            VFT2_UNKNOWN = 0x00000000 // The font type is unknown by the system.
        }

        #endregion


        #region Structures
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        private struct VS_VERSIONINFO
        {
            public ushort wLength;
            public ushort wValueLength;
            public ushort wType;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string szKey; // The Unicode string L"VS_VERSION_INFO". 
            
            public ushort Padding1;
            public VS_FIXEDFILEINFO Value;
            public ushort Padding2;
            public ushort Children;

            public VS_VERSIONINFO(BinaryReader reader)
            {
                VS_VERSIONINFO hdr = new VS_VERSIONINFO();
                this = hdr;

                byte[] buffer = new byte[Marshal.SizeOf(this)];
                reader.Read(buffer, 0, buffer.Length);
                hdr = buffer.ToStructure<VS_VERSIONINFO>();

                //hdr.Value.FileVersion = hdr.Value.FileVersion.ReverseBytes();
                //hdr.Value.ProductVersion = hdr.Value.ProductVersion.ReverseBytes();
                //hdr.Value.TimeStamp =hdr.Value.TimeStamp.ReverseBytes();

                this = hdr;                
            }            
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct VS_FIXEDFILEINFO
        {

            [FieldOffset(0)]
            public UInt32 dwSignature; // Contains the value 0xFEEF04BD. 
            [FieldOffset(4)]
            public UInt32 dwStrucVersion;

            [FieldOffset(8)]
            public UInt32 dwFileVersionMS;
            [FieldOffset(12)]
            public UInt32 dwFileVersionLS;
            [FieldOffset(8)]
            public UInt64 FileVersion;

            [FieldOffset(16)]
            public UInt32 dwProductVersionMS;
            [FieldOffset(20)]
            public UInt32 dwProductVersionLS;
            [FieldOffset(16)]
            public UInt64 ProductVersion;

            [FieldOffset(24)]
            public UInt32 dwFileFlagsMask;

            [FieldOffset(28)]
            public FileFlags dwFileFlags;
            [FieldOffset(32)]
            public FileOS dwFileOS;
            [FieldOffset(36)]
            public FileType dwFileType;

            [FieldOffset(40)]
            public FileDRVSubtype DriverFileSubtype;
            [FieldOffset(40)]
            public FileFNTSubtype FontFileSubtype;
            [FieldOffset(40)]
            public UInt32 VXDFileSubtype;

            [FieldOffset(44)]
            public UInt32 dwFileDateMS;
            [FieldOffset(48)]
            public UInt32 dwFileDateLS;
            [FieldOffset(44)]
            public UInt64 TimeStamp;
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        private struct VarFileInfo
        {
            public ushort wLength;
            public ushort wValueLength;
            public ushort wType;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
            public string szKey; // The Unicode string L"VarFileInfo".
            public ushort Padding;
            Var Children;


            public VarFileInfo(BinaryReader reader)
            {
                VarFileInfo hdr = new VarFileInfo();
                this = hdr;

                byte[] buffer = new byte[Marshal.SizeOf(this)];
                reader.Read(buffer, 0, buffer.Length);
                hdr = buffer.ToStructure<VarFileInfo>();

                this = hdr;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        private struct Var 
        {
            public ushort wLength;
            public ushort wValueLength;
            public ushort wType;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
            public string szKey; // The Unicode string L"Translation". 
            public ushort Padding;
            public UInt32 Value;            
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        private struct StringTable
        {
            public ushort wLength;
            public ushort wValueLength;
            public ushort wType;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string szKey; // An 8-digit hexadecimal number stored as a Unicode string.
            public ushort Padding;
            VI_String Children;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        private struct StringFileInfo
        {
            public ushort wLength;
            public ushort wValueLength;
            public ushort wType;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string szKey; // L"StringFileInfo". 
            public ushort Padding;
            public StringTable Children;


            public StringFileInfo(BinaryReader reader)
            {
                StringFileInfo hdr = new StringFileInfo();
                this = hdr;

                byte[] buffer = new byte[Marshal.SizeOf(this)];
                reader.Read(buffer, 0, buffer.Length);
                hdr = buffer.ToStructure<StringFileInfo>();                              

                this = hdr;                
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        private struct VI_String
        {
            public ushort wLength;
            public ushort wValueLength;
            public ushort wType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)] // Unknown size
            public char[] szKey; //
            public ushort Padding;
            public ushort Value;
        }
        #endregion


        #region Constructors / Destructors
        public FileVersionInfo(byte[] buffer)
        {
            // Constructor
            _ms = new MemoryStream(buffer);

            // VS_VERSION_INFO
            byte[] pattern = Encoding.Unicode.GetBytes("VS_VERSION_INFO");            
            long pos = Search.IndexOf(buffer, pattern);
            if (pos == -1)
            {
                return; // No VersionInfo found
            }
            
            _ms.Position = pos - 6; // setup the correct offset            
            vi = new VS_VERSIONINFO(new BinaryReader(_ms));
                       
            //_ms.Position -= 6;
            //VarFileInfo sfi = new VarFileInfo(new BinaryReader(_ms));
        }

        ~FileVersionInfo()
        { 
            // Destructor
            if (_ms != null)
            {
                _ms.Close();
                _ms.Dispose();
            }
        }
        #endregion

        #region Properties
        public string FileVersion
        {
            get 
            {
                ushort major  = 0;
                ushort minor  = 0;                
                ushort release = 0;
                ushort build = 0;

                if (vi.Value.dwSignature == 0xFEEF04BD)
                {
                    major = vi.Value.dwFileVersionMS.GetWords().HI;
                    minor = vi.Value.dwFileVersionMS.GetWords().LOW;

                    release = vi.Value.dwFileVersionLS.GetWords().HI;
                    build = vi.Value.dwFileVersionLS.GetWords().LOW;
                }
                
                return String.Format("{0}.{1}.{2}.{3}", major, minor, release, build);            
            }
        }

        public string ProductVersion
        {
            get
            {
                ushort major = 0;
                ushort minor = 0;
                ushort release = 0;
                ushort build = 0;

                if (vi.Value.dwSignature == 0xFEEF04BD)
                {
                    major = vi.Value.dwProductVersionMS.GetWords().HI;
                    minor = vi.Value.dwProductVersionMS.GetWords().LOW;

                    release = vi.Value.dwProductVersionLS.GetWords().HI;
                    build = vi.Value.dwProductVersionLS.GetWords().LOW;
                }

                return String.Format("{0}.{1}.{2}.{3}", major, minor, release, build);
            }
        }
        #endregion
    }

    public class AnalyzeAssembly
    {
        /// <summary>
        /// Create an app domain
        /// </summary>
        /// <returns></returns>
        private static AppDomain GetTempAppDomain()
        {
            //copy the current app domain setup but don't shadow copy files
            var appName = "TempDomain" + Guid.NewGuid();
            var domainSetup = new AppDomainSetup
            {
                ApplicationName = appName,
                ShadowCopyFiles = "false",
                ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
                DynamicBase = AppDomain.CurrentDomain.SetupInformation.DynamicBase,
                LicenseFile = AppDomain.CurrentDomain.SetupInformation.LicenseFile,
                LoaderOptimization = AppDomain.CurrentDomain.SetupInformation.LoaderOptimization,
                PrivateBinPath = AppDomain.CurrentDomain.SetupInformation.PrivateBinPath,
                PrivateBinPathProbe = AppDomain.CurrentDomain.SetupInformation.PrivateBinPathProbe
            };

            //create new domain with full trust
            //return AppDomain.CreateDomain(appName, AppDomain.CurrentDomain.Evidence, domainSetup, new PermissionSet(PermissionState.Unrestricted));
            return AppDomain.CreateDomain(appName);
        }

        #region Constructors / Destructors
        public AnalyzeAssembly(byte[] RawData)
        {
            AppDomain appDomain = GetTempAppDomain();
            // Constructor          
            try
            {
                // To dynamically load and unload assemblies for analysis we each have to load them in its own AppDomain
                // When a AppDomain is unloaded, each associated assembly is unloaded as well.
                // https://docs.microsoft.com/de-de/dotnet/csharp/programming-guide/concepts/assemblies-gac/how-to-load-and-unload-assemblies
                // AppDomain Example from https://gist.github.com/Shazwazza/7147978
                var type = typeof(AssemblyLoader);
                var value = (AssemblyLoader)appDomain.CreateInstanceAndUnwrap(type.Assembly.FullName, type.FullName);
                value.Load(RawData);

                _isDotNetFile = value.IsDotNetFile;
                _isObfuscated = value.IsObfuscated;
                _obfuscationPercentage = value.ObfuscationPercentage;
            }
            catch (Exception ex)
            {
                //Console.WriteLine("General Exception");
                _obfuscationPercentage = 0.0;
                _isDotNetFile = false;
                _isObfuscated = false;
            }
            finally
            {
                AppDomain.Unload(appDomain);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        ~AnalyzeAssembly()
        {
            
        }

        private bool _isObfuscated;
        public bool IsObfuscated
        {
            get
            {
                return _isObfuscated;
            }
        }

        private double _obfuscationPercentage;
        public double ObfuscationPercentage
        {
            get
            {
                return _obfuscationPercentage;
            }
        }

        private bool _isDotNetFile;
        public bool IsDotNetFile
        {
            get
            {
                return _isDotNetFile;
            }
        }
        #endregion
    }

    [Serializable]
    public class AssemblyLoader
    {
        // This Class runs in its own AppDomain, loads an assembly and analyzes it
        // Afterwards the AppDomain is unloaded.
        // The Class must be marked as [Serializable]!

        private bool _isObfuscated = false;
        public bool IsObfuscated => _isObfuscated;

        private double _obfuscationPercentage;
        public double ObfuscationPercentage => _obfuscationPercentage;

        private bool _isDotNetFile;
        public bool IsDotNetFile => _isDotNetFile;

        public void Load(byte[] buffer)
        {
            try
            {
                System.Reflection.Assembly asm = AppDomain.CurrentDomain.Load(buffer);
                _isDotNetFile = true;

                Type[] types = new Type[] { };
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Get the loaded types and ignore the rest
                    types = ex.Types;
                }
                catch (Exception)
                { }

                uint cnt = 0;
                long len = types.LongLength;

                #region Analysis
                foreach (Type t in types)
                {
                    try
                    {

                        if (t != null)
                        {
                            if (t.Name.Length < 2 || t.Name == "DotfuscatorAttribute")
                            {
                                // Type seems to be obfuscated
                                cnt++;
                            }

                            MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            len += methods.LongLength;
                            foreach (MethodInfo m in methods)
                            {
                                try
                                {
                                    if ((m.Name == "$") || (m.Name.Length < 2) || m.Name.Contains("="))
                                    {
                                        //Method seems to be obfuscated
                                        cnt++;
                                    }
                                }
                                catch (Exception)
                                {
                                }
                            }

                            PropertyInfo[] pis = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            len += pis.LongLength;
                            foreach (PropertyInfo pi in pis)
                            {
                                try
                                {
                                    if ((pi.Name.Length < 2) || pi.Name.Contains("="))
                                    {
                                        // Property seems to be obfuscated
                                        cnt++;
                                    }
                                }
                                catch (Exception)
                                {
                                }
                            }

                            FieldInfo[] fis = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            len += fis.LongLength;
                            foreach (FieldInfo fi in fis)
                            {
                                try
                                {
                                    if ((fi.Name.Length < 2) || fi.Name.Contains("="))
                                    {
                                        // Field seems to be obfuscated
                                        cnt++;
                                    }
                                }
                                catch (Exception)
                                {
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }

                }
                #endregion

                double x = ((double)cnt * 100) / len;


                if (x > 0.0)
                {
                    _isObfuscated = true;
                    _obfuscationPercentage = x;
                }
                else if (Double.IsNaN(x))
                {
                    _obfuscationPercentage = 0.0;
                    _isObfuscated = false;
                }
                else
                {
                    _obfuscationPercentage = x;
                    _isObfuscated = false;
                }
            }
            catch (ReflectionTypeLoadException)
            {
                //Console.WriteLine("ReflectionTypeLoadException");
                _obfuscationPercentage = 0.0;
                _isDotNetFile = false;
                _isObfuscated = false;
            }
            catch (Exception ex)
            {
                //Console.WriteLine("General Exception");
                _obfuscationPercentage = 0.0;
                _isDotNetFile = false;
                _isObfuscated = false;
            }
        }   
    }
}
