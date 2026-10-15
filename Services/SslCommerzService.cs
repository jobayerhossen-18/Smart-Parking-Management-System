using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SmartParkingMS.Services
{
    public class SslCommerzOptions
    {
        public string StoreId { get; set; } = "";
        public string StorePassword { get; set; } = "";
        public bool Sandbox { get; set; } = true;
        public string BaseUrl { get; set; } = "https://localhost:5001";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(StoreId) && !string.IsNullOrWhiteSpace(StorePassword);
        public string GatewayUrl => Sandbox
            ? "https://sandbox.sslcommerz.com/gwprocess/v4/api.php"
            : "https://securepay.sslcommerz.com/gwprocess/v4/api.php";
        public string ValidationUrl => Sandbox
            ? "https://sandbox.sslcommerz.com/validator/api/validationserverAPI.php"
            : "https://securepay.sslcommerz.com/validator/api/validationserverAPI.php";
    }

    public record GatewayInitResult(bool Success, string? GatewayPageUrl, string? ErrorMessage);
    public record GatewayValidationResult(bool Success, decimal Amount, string Status, string? TransactionId, string? Currency, string? ErrorMessage);

    public class SslCommerzService
    {
        private readonly HttpClient _http;
        private readonly SslCommerzOptions _options;

        public SslCommerzService(HttpClient http, IOptions<SslCommerzOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        public async Task<GatewayInitResult> InitiateAsync(int paymentId, decimal amount, string customerName, string email, string bookingId)
        {
            if (!_options.IsConfigured) return new(false, null, "SSLCOMMERZ credentials are not configured.");

            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var values = new Dictionary<string, string>
            {
                ["store_id"] = _options.StoreId,
                ["store_passwd"] = _options.StorePassword,
                ["total_amount"] = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                ["currency"] = "BDT",
                ["tran_id"] = $"SPPAY-{paymentId}",
                ["success_url"] = $"{baseUrl}/Payment/GatewaySuccess",
                ["fail_url"] = $"{baseUrl}/Payment/GatewayFail",
                ["cancel_url"] = $"{baseUrl}/Payment/GatewayCancel",
                ["cus_name"] = customerName,
                ["cus_email"] = string.IsNullOrWhiteSpace(email) ? "customer@smartparking.local" : email,
                ["cus_add1"] = "Smart Parking",
                ["cus_city"] = "Dhaka",
                ["cus_country"] = "Bangladesh",
                ["shipping_method"] = "NO",
                ["product_name"] = "Smart Parking Service",
                ["product_category"] = "Parking",
                ["product_profile"] = "general",
                ["value_a"] = bookingId
            };

            try
            {
                using var response = await _http.PostAsync(_options.GatewayUrl, new FormUrlEncodedContent(values));
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) return new(false, null, $"Gateway returned HTTP {(int)response.StatusCode}.");

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
                var url = root.TryGetProperty("GatewayPageURL", out var gp) ? gp.GetString() : null;
                if (string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(url))
                    return new(true, url, null);

                var failed = root.TryGetProperty("failedreason", out var fr) ? fr.GetString() : "Unable to create gateway session.";
                return new(false, null, failed);
            }
            catch (Exception ex)
            {
                return new(false, null, "Could not contact the payment gateway. Check server logs and gateway configuration.");
            }
        }

        public async Task<GatewayValidationResult> ValidateAsync(string valId)
        {
            if (!_options.IsConfigured) return new(false, 0, "NOT_CONFIGURED", null, null, "SSLCOMMERZ credentials are not configured.");
            try
            {
                var url = $"{_options.ValidationUrl}?val_id={Uri.EscapeDataString(valId)}&store_id={Uri.EscapeDataString(_options.StoreId)}&store_passwd={Uri.EscapeDataString(_options.StorePassword)}&format=json";
                using var response = await _http.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) return new(false, 0, "FAILED", null, null, "Validation request failed.");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
                var amountText = root.TryGetProperty("amount", out var a) ? a.ToString() : "0";
                decimal.TryParse(amountText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount);
                var transactionId = root.TryGetProperty("tran_id", out var tid) ? tid.GetString() : null;
                var currency = root.TryGetProperty("currency_type", out var cur) ? cur.GetString() : null;
                return new(status is "VALID" or "VALIDATED", amount, status, transactionId, currency, null);
            }
            catch (Exception ex)
            {
                return new(false, 0, "FAILED", null, null, "Payment validation could not be completed.");
            }
        }
    }
}
