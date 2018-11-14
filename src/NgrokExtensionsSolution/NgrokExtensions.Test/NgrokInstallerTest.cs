using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RichardSzalay.MockHttp;

namespace NgrokExtensions.Test
{
    [TestClass]
    public class NgrokInstallerTest
    {
        private readonly HttpClient _mockHttpClient;
        private MockHttpMessageHandler _mockHttpMessageHandler;

        public NgrokInstallerTest()
        {
            _mockHttpMessageHandler = new MockHttpMessageHandler();
            _mockHttpMessageHandler.When("https://ngrok.com/download").Respond("text/html", TestResponseContent);

            var stream = new MemoryStream(SampleZip);
            _mockHttpMessageHandler.When("https://fakedomain.io/ngrok64.zip").Respond("application/zip", stream);
            _mockHttpMessageHandler.When("https://fakedomain.io/ngrok32.zip").Respond("application/zip", stream);

            _mockHttpClient = _mockHttpMessageHandler.ToHttpClient();
        }

        [TestMethod]
        public async Task TestGetNgrokDownloadUrl()
        {
            var installer = new NgrokInstaller(_mockHttpClient, true);
            var url = await installer.GetNgrokDownloadUrl();
            Assert.AreEqual("https://fakedomain.io/ngrok64.zip", url);
        }

        [TestMethod]
        public async Task TestGetNgrokDownloadUrl32Bit()
        {
            var installer = new NgrokInstaller(_mockHttpClient, false);
            var url = await installer.GetNgrokDownloadUrl();
            Assert.AreEqual("https://fakedomain.io/ngrok32.zip", url);
        }

        [TestMethod]
        [ExpectedException(typeof(NgrokDownloadException))]
        public async Task TestGetNgrokDownloadUrlHttpError()
        {
            _mockHttpMessageHandler = new MockHttpMessageHandler();
            _mockHttpMessageHandler.When("https://ngrok.com/download")
                .Respond(x => new HttpResponseMessage(HttpStatusCode.NotFound));
            var installer = new NgrokInstaller(_mockHttpMessageHandler.ToHttpClient(), false);
            await installer.GetNgrokDownloadUrl();
        }

        [TestMethod]
        [ExpectedException(typeof(NgrokDownloadException))]
        public async Task TestGetNgrokDownloadUrlTextNotFound()
        {
            _mockHttpMessageHandler = new MockHttpMessageHandler();
            _mockHttpMessageHandler.When("https://ngrok.com/download")
                .Respond("text/html", "<h1>some html without expected download links</h1>");
            var installer = new NgrokInstaller(_mockHttpMessageHandler.ToHttpClient(), false);
            await installer.GetNgrokDownloadUrl();
        }

        [TestMethod]
        public async Task TestDownloadNgrok()
        {
            var installer = new NgrokInstaller(_mockHttpClient, true);
            var stream = await installer.DownloadNgrok();
            var buffer = new byte[SampleZip.Length];
            await stream.ReadAsync(buffer, 0, SampleZip.Length);
            Assert.IsTrue(buffer.SequenceEqual(SampleZip));
        }

        [TestMethod]
        [ExpectedException(typeof(NgrokDownloadException))]
        public async Task TestDownloadNgrokFailed()
        {
            _mockHttpMessageHandler = new MockHttpMessageHandler();
            _mockHttpMessageHandler.When("https://fakedomain.io/ngrok.zip")
                .Respond(x => new HttpResponseMessage(HttpStatusCode.NotFound));
            var installer = new NgrokInstaller(_mockHttpMessageHandler.ToHttpClient(), false);

            await installer.DownloadNgrok("https://fakedomain.io/ngrok.zip");
        }

        [TestMethod]
        public async Task TestInstallNgrok()
        {
            var installer = new NgrokInstaller(_mockHttpClient, true);
            var path = await installer.InstallNgrok();
            Assert.IsTrue(Regex.IsMatch(path, @"^.*\\ngrok\.exe$"));
            Assert.IsTrue(File.Exists(path));
            File.Delete(path);
        }

        private const string TestResponseContent = @"<section class=""container-fluid-wide download-page"">
  <div class=""jumbotron"" style=""margin-bottom: 0em;"">
    <h1>Download &amp; setup ngrok</h1>
    <p class=""lead"">
      Get started with ngrok in just a few seconds.
    </p>
  </div>

  <ol class=""download-instructions unstyled"">
    <li class=""card-container"">
      <h4>Download ngrok</h4>
      <p>
        First, download the ngrok client, a single binary with zero run-time dependencies.
      </p>
      <div class=""download-buttons"" id=""download"">
  <a id=""dl-darwin-amd64"" href=""https://bin.equinox.io/c/4VmDzA7iaHb/ngrok-stable-darwin-amd64.zip"" class=""download-btn"" onclick=""ga('send', 'event', 'ngrok', 'Downloaded', 'darwin_amd64');"">
    <span>Mac OS X</span>
  </a>
  <a id=""dl-windows-amd64"" href=""https://fakedomain.io/ngrok64.zip"" class=""download-btn"" onclick=""ga('send', 'event', 'ngrok', 'Downloaded', 'windows_amd64');"">
    <span>Windows</span>
  </a>
  <a id=""dl-linux-amd64"" href=""https://bin.equinox.io/c/4VmDzA7iaHb/ngrok-stable-linux-amd64.zip"" class=""download-btn"" onclick=""ga('send', 'event', 'ngrok', 'Downloaded', 'linux_amd64');"">
    <span>Linux</span>
  </a>
  <a id=""dl-darwin-386"" href=""https://bin.equinox.io/c/4VmDzA7iaHb/ngrok-stable-darwin-386.zip"" class=""download-btn"" onclick=""ga('send', 'event', 'ngrok', 'Downloaded', 'darwin_386');"">
    Mac (32-bit)
  </a>
  <a id=""dl-windows-386"" href=""https://fakedomain.io/ngrok32.zip"" class=""download-btn"" onclick=""ga('send', 'event', 'ngrok', 'Downloaded', 'windows_386');"">
    Windows (32-bit)
  </a>
  <a id=""dl-linux-arm"" href=""https://bin.equinox.io/c/4VmDzA7iaHb/ngrok-stable-linux-arm.zip"" class=""download-btn"" onclick=""ga('send', 'event', 'ngrok', 'Downloaded', 'linux_arm');"">
    Linux (ARM)
  </a>
  <a id=""dl-linux-arm"" href=""https://bin.equinox.io/a/nmkK3DkqZEB/ngrok-2.2.8-linux-arm64.zip"" class=""download-btn"" onclick=""ga('send', 'event', 'ngrok', 'Downloaded', 'linux_arm');"">
    Linux (ARM64)
  </a>
  <a id=""dl-linux-386"" href=""https://bin.equinox.io/c/4VmDzA7iaHb/ngrok-stable-linux-386.zip"" class=""download-btn"" onclick=""ga('send', 'event', 'ngrok', 'Downloaded', 'linux_386');"">
    Linux (32-bit)
  </a>
  
    <a id=""dl-freebsd-amd64"" href=""https://bin.equinox.io/c/4VmDzA7iaHb/ngrok-stable-freebsd-amd64.zip"" class=""download-btn"" onclick=""ga('send', 'event', 'ngrok', 'Downloaded', 'freebsd_amd64');"">
      FreeBSD (64-Bit)
    </a>
  
  
    <a id=""dl-freebsd-386"" href=""https://bin.equinox.io/c/4VmDzA7iaHb/ngrok-stable-freebsd-386.zip"" class=""download-btn"" onclick=""ga('send', 'event', 'ngrok', 'Downloaded', 'freebsd_386');"">
      FreeBSD (32-bit)
    </a>
  
</div>";

        // ZIP file with a single "ngrok.exe" entry
        private static readonly byte[] SampleZip = { 0x50, 0x4B, 0x03, 0x04, 0x0A, 0, 0, 0, 0, 0, 0xED, 0x6E, 0xC8, 0x4A, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x09, 0, 0, 0, 0x6E, 0x67, 0x72, 0x6F, 0x6B, 0x2E, 0x65, 0x78, 0x65, 0x50, 0x4B, 0x01, 0x02, 0x3F, 0, 0x0A, 0, 0, 0, 0, 0, 0xED, 0x6E, 0xC8, 0x4A, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x09, 0, 0x24, 0, 0, 0, 0, 0, 0, 0, 0x20, 0, 0, 0, 0, 0, 0, 0, 0x6E, 0x67, 0x72, 0x6F, 0x6B, 0x2E, 0x65, 0x78, 0x65, 0x0A, 0, 0x20, 0, 0, 0, 0, 0, 0x01, 0, 0x18, 0, 0xF2, 0xF2, 0x5B, 0x8D, 0x99, 0xE0, 0xD2, 0x01, 0xF2, 0xF2, 0x5B, 0x8D, 0x99, 0xE0, 0xD2, 0x01, 0xF2, 0xF2, 0x5B, 0x8D, 0x99, 0xE0, 0xD2, 0x01, 0x50, 0x4B, 0x05, 0x06, 0, 0, 0, 0, 0x01, 0, 0x01, 0, 0x5B, 0, 0, 0, 0x27, 0, 0, 0, 0, 0 };
    }
}
