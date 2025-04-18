using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Tsintra.Api.Controllers
{
    [ApiController]
    [Route("api/crm")]
    public class CrmProxyController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CrmProxyController> _logger;
        private readonly string _crmApiBaseUrl;

        public CrmProxyController(HttpClient httpClient, IConfiguration configuration, ILogger<CrmProxyController> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _crmApiBaseUrl = configuration["Services:CrmApi:BaseUrl"] ?? "http://localhost:5176";
        }

        [HttpGet]
        [Route("{**catchAll}")]
        public async Task<IActionResult> ProxyGet()
        {
            return await ForwardRequest(HttpMethod.Get);
        }

        [HttpPost]
        [Route("{**catchAll}")]
        public async Task<IActionResult> ProxyPost()
        {
            return await ForwardRequest(HttpMethod.Post);
        }

        [HttpPut]
        [Route("{**catchAll}")]
        public async Task<IActionResult> ProxyPut()
        {
            return await ForwardRequest(HttpMethod.Put);
        }

        [HttpDelete]
        [Route("{**catchAll}")]
        public async Task<IActionResult> ProxyDelete()
        {
            return await ForwardRequest(HttpMethod.Delete);
        }

        private async Task<IActionResult> ForwardRequest(HttpMethod method)
        {
            try
            {
                // Get the path that was requested (without the /api/crm prefix)
                var path = Request.Path.Value?.Replace("/api/crm", "") ?? "";
                var queryString = Request.QueryString.Value ?? "";

                // Construct the CRM API URL
                var url = $"{_crmApiBaseUrl}/api{path}{queryString}";
                _logger.LogInformation("Forwarding {Method} request to CRM API: {Url}", method, url);

                // Create the HTTP request message
                var request = new HttpRequestMessage(method, url);

                // Copy the request headers
                foreach (var header in Request.Headers)
                {
                    if (!header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                }

                // Copy the request body for POST/PUT requests
                if (method != HttpMethod.Get && method != HttpMethod.Delete && Request.ContentLength > 0)
                {
                    // Read request body
                    string requestBody;
                    using (var reader = new System.IO.StreamReader(Request.Body, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
                    {
                        requestBody = await reader.ReadToEndAsync();
                        // Reset the request body position
                        Request.Body.Position = 0;
                    }

                    request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                }

                // Send the request to the CRM API
                var response = await _httpClient.SendAsync(request);

                // Copy the response status code, headers, and content to our response
                Response.StatusCode = (int)response.StatusCode;

                foreach (var header in response.Headers)
                {
                    Response.Headers[header.Key] = header.Value.ToArray();
                }

                if (response.Content != null)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return Content(responseContent, "application/json");
                }

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding request to CRM API");
                return StatusCode(500, $"Error forwarding request: {ex.Message}");
            }
        }
    }
} 