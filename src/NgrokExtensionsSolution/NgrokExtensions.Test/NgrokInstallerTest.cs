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

        private const string TestResponseContent = @"<h2 id=""download"">Download and Installation</h2>
        <p>ngrok is easy to install. Download a single binary with <em>zero run-time dependencies</em> for any major platform. Unzip it and then run it from the command line.</p>
        <h4>Step 1: Download ngrok</h4>
        <table id=""dl"" class=""table"">
        <tr id=""dl-darwin-amd64"">
            <th>Mac OS X 64-Bit</th>
            <td>
                <span class=""pull-right"">
                    <a href=""https://bin.equinox.io/c/4VmDzA7iaHb/ngrok-stable-darwin-amd64.zip"" class=""btn btn-inverse"">
                    <i class=""icon-white icon-download""></i> Download
                </a>
                </span>
            </td>
        </tr>
        <tr id=""dl-windows-amd64"">
            <th>Windows 64-Bit</th>
            <td>
                <span class=""pull-right"">
                    <a href=""https://fakedomain.io/ngrok64.zip"" class=""btn btn-inverse"">
                    <i class=""icon-white icon-download""></i> Download
                </a>
                </span>
            </td>
        </tr>
        <tr id=""dl-linux-amd64"">
            <th>Linux 64-Bit</th>
            <td>
                <span class=""pull-right"">
                    <a href=""https://bin.equinox.io/c/4VmDzA7iaHb/ngrok-stable-linux-amd64.zip"" class=""btn btn-inverse"">
                    <i class=""icon-white icon-download""></i> Download
                </a>
                </span>
            </td>
        </tr>
        <tr id=""dl-linux-arm"">
            <th>Linux ARM</th>
            <td>
                <span class=""pull-right"">
                    <a href=""https://bin.equinox.io/c/4VmDzA7iaHb/ngrok-stable-linux-arm.zip"" class=""btn btn-inverse"">
                    <i class=""icon-white icon-download""></i> Download
                </a>
                </span>
            </td>
        </tr>
         <tr id=""dl-windows-386"" class=""hide"">
            <th>Windows 32-bit</th>
            <td>
                <span class=""pull-right"">
                    <a href=""https://fakedomain.io/ngrok32.zip"" 
                    class=""btn btn-inverse"">
                    <i class=""icon-white icon-download""></i> Download
                </a>
                </span>
            </td>
        </tr>";

        // ZIP file with a single "ngrok.exe" entry
        private static readonly byte[] SampleZip = { 0x50, 0x4B, 0x03, 0x04, 0x0A, 0, 0, 0, 0, 0, 0xED, 0x6E, 0xC8, 0x4A, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x09, 0, 0, 0, 0x6E, 0x67, 0x72, 0x6F, 0x6B, 0x2E, 0x65, 0x78, 0x65, 0x50, 0x4B, 0x01, 0x02, 0x3F, 0, 0x0A, 0, 0, 0, 0, 0, 0xED, 0x6E, 0xC8, 0x4A, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x09, 0, 0x24, 0, 0, 0, 0, 0, 0, 0, 0x20, 0, 0, 0, 0, 0, 0, 0, 0x6E, 0x67, 0x72, 0x6F, 0x6B, 0x2E, 0x65, 0x78, 0x65, 0x0A, 0, 0x20, 0, 0, 0, 0, 0, 0x01, 0, 0x18, 0, 0xF2, 0xF2, 0x5B, 0x8D, 0x99, 0xE0, 0xD2, 0x01, 0xF2, 0xF2, 0x5B, 0x8D, 0x99, 0xE0, 0xD2, 0x01, 0xF2, 0xF2, 0x5B, 0x8D, 0x99, 0xE0, 0xD2, 0x01, 0x50, 0x4B, 0x05, 0x06, 0, 0, 0, 0, 0x01, 0, 0x01, 0, 0x5B, 0, 0, 0, 0x27, 0, 0, 0, 0, 0 };
    }
}
