﻿using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using RichardSzalay.MockHttp;

namespace NgrokExtensions.Test
{
    [TestClass]
    public class NgrokUtilsTest
    {
        private MockHttpMessageHandler _mockHttp;
        private HttpClient _client;
        private Mock<IErrorDisplayFunc> _mockErrorDisplay;
        private FakeNgrokProcess _ngrokProcess;
        private NgrokUtils _utils;
        private Dictionary<string, WebAppConfig> _webApps;
        private NgrokTunnelApiRequest _expectedRequest;
        private NgrokTunnelsApiResponse _emptyTunnelsResponse;
        private int _expectedProcessCount;
        private string _tempFile;

        [TestInitialize]
        public void Initialize()
        {
            _expectedProcessCount = 0;
            _tempFile = Path.GetTempFileName();
            _mockHttp = new MockHttpMessageHandler();
            _client = new HttpClient(_mockHttp);

            _webApps = new Dictionary<string, WebAppConfig>
            {
                {
                    "fakeApp",
                    new WebAppConfig("1234")
                    {
                        SubDomain = "fake-app",
                        HostName = "dev.fake-app.com",
                        Region = "us"
                    }
                }
            };

            _mockErrorDisplay = new Mock<IErrorDisplayFunc>();
            _mockErrorDisplay.Setup(x => x.ShowErrorAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(0))
                .Verifiable("Error display not called.");

            InitializeUtils("ngrok version 2.3.34\r\n");

            _emptyTunnelsResponse = new NgrokTunnelsApiResponse
            {
                tunnels = new Tunnel[0],
                uri = ""
            };

            _expectedRequest = new NgrokTunnelApiRequest
            {
                addr = "localhost:1234",
                host_header = "localhost:1234",
                name = "fakeApp",
                proto = "http",
                subdomain = "fake-app",
                hostname = "dev.fake-app.com"
            };
        }

        private void InitializeUtils(string stdout)
        {
            _ngrokProcess = new FakeNgrokProcess(_tempFile, stdout);
            _utils = new NgrokUtils(_webApps, _tempFile, _mockErrorDisplay.Object.ShowErrorAsync, _client, _ngrokProcess);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if(File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
            Assert.AreEqual(_expectedProcessCount, _ngrokProcess.StartCount);
            _mockHttp.VerifyNoOutstandingExpectation();
        }

        [TestMethod]
        public async Task TestStartTunnelAsync()
        {
            _mockHttp.Expect("http://localhost:4040/api/tunnels")
                .Respond("application/json", JsonConvert.SerializeObject(_emptyTunnelsResponse));

            _mockHttp.Expect(HttpMethod.Post, "http://localhost:4040/api/tunnels")
                .WithContent(JsonConvert.SerializeObject(_expectedRequest))
                .Respond("application/json", "{}");

            await _utils.StartTunnelsAsync();
        }

        [TestMethod]
        public async Task TestStartTunnelNotRunningAsync()
        {
            _mockHttp.Expect("http://localhost:4040/api/tunnels")
                .Respond(HttpStatusCode.BadGateway);

            _mockHttp.Expect("http://localhost:4040/api/tunnels")
                .Respond("application/json", JsonConvert.SerializeObject(_emptyTunnelsResponse));

            _mockHttp.Expect(HttpMethod.Post, "http://localhost:4040/api/tunnels")
                .WithContent(JsonConvert.SerializeObject(_expectedRequest))
                .Respond("application/json", "{}");

            await _utils.StartTunnelsAsync();

            _expectedProcessCount = 1;
        }

        [TestMethod]
        public async Task TestStartTunnelExistingAsync()
        {
            var tunnels = new NgrokTunnelsApiResponse
            {
                tunnels = new[]
                {
                    new Tunnel
                    {
                        config = new Config
                        {
                            addr = "localhost:1234",
                            inspect = true
                        },
                        name = "fakeApp",
                        proto = "https",
                        public_url = "https://fake-app.ngrok.io"
                    },
                    new Tunnel
                    {
                        config = new Config
                        {
                            addr = "localhost:1234",
                            inspect = true
                        },
                        name = "fakeApp",
                        proto = "http",
                        public_url = "http://fake-app.ngrok.io"
                    }
                },
                uri = ""
            };

            _mockHttp.Expect("http://localhost:4040/api/tunnels")
                .Respond("application/json", JsonConvert.SerializeObject(tunnels));

            await _utils.StartTunnelsAsync();
        }

        [TestMethod]
        public void TestNgrokIsInstalled()
        {
            Assert.AreEqual(true, _utils.NgrokIsInstalled());
            _expectedProcessCount = 1;
        }

        [TestMethod]
        public void TestNgrokOldVersion()
        {
            InitializeUtils("ngrok version 2.3.32\r\n");
            Assert.AreEqual(false, _utils.NgrokIsInstalled());
            _expectedProcessCount = 1;
        }

        [TestMethod]
        public void TestNgrokNewerVersion()
        {
            InitializeUtils("ngrok version 3.0.1\r\n");
            Assert.AreEqual(true, _utils.NgrokIsInstalled());
            _expectedProcessCount = 1;
        }
    }
}
