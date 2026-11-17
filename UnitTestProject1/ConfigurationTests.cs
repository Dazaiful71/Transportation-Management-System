using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySql.Data.MySqlClient;
using Transportation_Management_System;

namespace UnitTestProject1
{
    [TestClass]
    public class ConfigurationTests
    {
        private string tempDirectory;
        private string originalBaseDirectory;

        [TestInitialize]
        public void SetUp()
        {
            originalBaseDirectory = AppSettings.BaseDirectory;
            tempDirectory = Path.Combine(Path.GetTempPath(), "tms-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            AppSettings.BaseDirectory = tempDirectory;
            AppSettings.Reload();
        }

        [TestCleanup]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("TMS_UNITTESTKEY", null);
            AppSettings.BaseDirectory = originalBaseDirectory;
            AppSettings.Reload();
            Directory.Delete(tempDirectory, true);
        }

        [TestMethod]
        public void Get_MissingKey_ReturnsNullOrDefault()
        {
            Assert.IsNull(AppSettings.Get("UnitTestKey"));
            Assert.AreEqual("fallback", AppSettings.Get("UnitTestKey", "fallback"));
            Assert.AreEqual(42, AppSettings.GetInt("UnitTestKey", 42));
        }

        [TestMethod]
        public void Get_ReadsLocalOverrideFile()
        {
            File.WriteAllText(Path.Combine(tempDirectory, AppSettings.LocalFileName),
                "<appSettings><add key=\"UnitTestKey\" value=\"from-file\" /></appSettings>");
            AppSettings.Reload();

            Assert.AreEqual("from-file", AppSettings.Get("UnitTestKey"));
            Assert.AreEqual("from-file", AppSettings.Get("unittestkey"), "Keys should be case-insensitive.");
        }

        [TestMethod]
        public void Get_EnvironmentVariableBeatsLocalFile()
        {
            File.WriteAllText(Path.Combine(tempDirectory, AppSettings.LocalFileName),
                "<appSettings><add key=\"UnitTestKey\" value=\"from-file\" /></appSettings>");
            Environment.SetEnvironmentVariable("TMS_UNITTESTKEY", "from-env");
            AppSettings.Reload();

            Assert.AreEqual("from-env", AppSettings.Get("UnitTestKey"));
        }

        [TestMethod]
        public void Get_BrokenLocalFile_IsIgnored()
        {
            File.WriteAllText(Path.Combine(tempDirectory, AppSettings.LocalFileName), "<appSettings><add key=");
            AppSettings.Reload();

            Assert.IsNull(AppSettings.Get("UnitTestKey"));
        }

        [TestMethod]
        public void GetInt_InvalidNumber_UsesDefault()
        {
            Environment.SetEnvironmentVariable("TMS_UNITTESTKEY", "not-a-number");

            Assert.AreEqual(3306, AppSettings.GetInt("UnitTestKey", 3306));
        }

        [TestMethod]
        public void Get_EmptyKey_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => AppSettings.Get(" "));
        }

        [TestMethod]
        public void ParseAppSettingsXml_AcceptsFullConfigurationDocument()
        {
            var values = AppSettings.ParseAppSettingsXml(
                "<configuration><appSettings><add key=\"Server\" value=\"db\" /><add key=\"Port\" value=\"3307\" /></appSettings></configuration>");

            Assert.AreEqual("db", values["Server"]);
            Assert.AreEqual("3307", values["port"]);
        }

        [TestMethod]
        public void BuildConnectionString_EscapesSpecialCharacters()
        {
            // A password with ';' must not be able to inject extra connection options.
            string connection = DAL.BuildConnectionString("db.local", "3307", "app", "p;ssw=rd;Database=other", "tms");
            var parsed = new MySqlConnectionStringBuilder(connection);

            Assert.AreEqual("db.local", parsed.Server);
            Assert.AreEqual(3307u, parsed.Port);
            Assert.AreEqual("app", parsed.UserID);
            Assert.AreEqual("p;ssw=rd;Database=other", parsed.Password);
            Assert.AreEqual("tms", parsed.Database);
            Assert.IsTrue(parsed.ConvertZeroDateTime);
        }

        [TestMethod]
        public void BuildConnectionString_InvalidPort_KeepsDefault()
        {
            var parsed = new MySqlConnectionStringBuilder(DAL.BuildConnectionString("db", "abc", "u", "p", "tms"));

            Assert.AreEqual(3306u, parsed.Port);
        }

        [TestMethod]
        public void ContractMarketPlace_Unconfigured_GivesClearError()
        {
            var marketplace = new ContractMarketPlace { CMPServer = null, UID = null };

            Assert.ThrowsException<System.Configuration.ConfigurationErrorsException>(() => marketplace.GetContracts());
        }
    }
}
