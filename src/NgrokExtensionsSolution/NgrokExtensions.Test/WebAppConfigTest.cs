using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NgrokExtensions.Test
{
    /// <summary>
    /// Summary description for WebAppConfigTest
    /// </summary>
    [TestClass]
    public class WebAppConfigTest
    {
        [TestMethod]
        public void TestUrlNoHttps()
        {
            var webApp = new WebAppConfig("http://localhost:1234/");
            Assert.AreEqual("localhost:1234", webApp.NgrokAddress);
        }

        [TestMethod]
        public void TestPortNumberOnly()
        {
            var webApp = new WebAppConfig("1234");
            Assert.AreEqual("localhost:1234", webApp.NgrokAddress);
        }

        [TestMethod]
        public void TestUrlWithHttps()
        {
            var webApp = new WebAppConfig("https://localhost:1234/");
            Assert.AreEqual("https://localhost:1234", webApp.NgrokAddress);
        }

        [TestMethod]
        public void TestUrlWithHttpsAndLongPath()
        {
            var webApp = new WebAppConfig("https://localhost:1234/foo/bar");
            Assert.AreEqual("https://localhost:1234", webApp.NgrokAddress);
        }

        [TestMethod]
        public void TestUrlWithHttpsAndNoPath()
        {
            var webApp = new WebAppConfig("https://localhost:1234");
            Assert.AreEqual("https://localhost:1234", webApp.NgrokAddress);
        }
    }
}
