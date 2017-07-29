using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MadMilkman.Ini;

namespace CSUpdater.Console
{
    public class Config
    {
        private IniFile _iniSettings;
        private const string IniFileName = "settings.ini";

        public string SourceRoot { get; private set; }

        public string TfsUserName { get; private set; }

        public string TfsPassword { get; private set; }

        public string ServerUrl { get; private set; }

        public bool Init()
        {
            var fileName = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), IniFileName);
            $"Ini file name : {fileName}".Info();
            if (!File.Exists(fileName))
            {
                throw new InvalidOperationException($"{fileName} file could not be located in rundir");
            }
            _iniSettings = new IniFile(
                new IniOptions()
                {
                    //    EncryptionPassword = "MySecretPassword"
                }
                )
                ;
            _iniSettings.Load(fileName);

            SourceRoot = _iniSettings.Sections["General"].Keys["SourceRoot"].Value;
            if (string.IsNullOrEmpty(SourceRoot))
            {
                $"{nameof(SourceRoot)} was null or empty".Error();
                return false;
            }
            TfsUserName = _iniSettings.Sections["Tfs"].Keys["Username"].Value;
            if (string.IsNullOrEmpty(TfsUserName))
            {
                $"{nameof(TfsUserName)} was null or empty".Error();
                return false;
            }

            TfsPassword = _iniSettings.Sections["Tfs"].Keys["Password"].Value;
            if (string.IsNullOrEmpty(TfsPassword))
            {
                $"{nameof(TfsPassword)} was null or empty".Error();
                return false;
            }
            ServerUrl = _iniSettings.Sections["Tfs"].Keys["ServerUrl"].Value;
            if (string.IsNullOrEmpty(ServerUrl))
            {
                $"{nameof(ServerUrl)} was null or empty".Error();
                return false;
            }
            return true;




        }
    }
}