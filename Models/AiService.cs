using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TTLooks.Models
{
    public class AiService
    {
        private readonly string _apiKey;

        private static readonly string[] CandidateModels = new string[]
        {
            "gemini-2.5-flash",
            "gemini-2.0-flash",
            "gemini-1.5-flash"
        };

        public AiService(IConfiguration configuration)
        {
            _apiKey = (configuration["GeminiApiKey"] ?? "").Trim();
        }

        /// <summary>
        /// 1. Trợ Lý AI Lập Kế Hoạch Du Lịch Chi Tiết
        /// </summary>
        public async Task<string> GenerateTravelPlanAsync(string destination, string days, string budget, string preferences)
        {
            string prompt = $@"Bạn là Chuyên gia tư vấn hành trình du lịch cao cấp của TTLooks.
Hãy lập một kế hoạch du lịch cực kỳ sống động và thực tế:
- Điểm đến: {destination}
- Thời lượng: {days}
- Ngân sách: {budget}
- Sở thích du khách: {preferences}

Yêu cầu định dạng:
1. Tóm tắt điểm nhấn nổi bật và thời gian lý tưởng trong năm.
2. Phân bổ chi tiết từng ngày (Sáng, Trưa, Chiều, Tối): gợi ý điểm tham quan, ẩm thực đặc sản nức tiếng và mẹo chụp ảnh đẹp.
3. Ước tính chi phí chi tiết (Đi lại, Vé, Ăn uống) và lưu ý trang phục, đặt vé trước.";

            var messages = new List<object>
            {
                new { role = "user", parts = new[] { new { text = prompt } } }
            };

            string? aiResult = await CallGeminiMultiTurnAsync(messages, 2000);
            return !string.IsNullOrWhiteSpace(aiResult) ? aiResult : GenerateFallbackPlan(destination, days, budget, preferences);
        }

        /// <summary>
        /// 2. Chatbot AI Du Lịch 24/7 Thông Minh (Hỗ trợ Hội thoại đa lượt / Multi-turn)
        /// </summary>
        public async Task<string> AskTravelChatbotWithHistoryAsync(List<ChatTurnDto> history, string currentMessage)
        {
            string systemInstruction = @"Bạn là Trợ lý AI du lịch thông minh, thân thiện và nhiệt tình của nền tảng TTLooks (tương tự trợ lý Klook tại Việt Nam).
Vai trò & Kỹ năng của bạn:
1. Phong cách trò chuyện: Xưng hô 'Em' và 'Bạn' (hoặc 'Anh/Chị'), lời văn dí dỏm, tự nhiên, tràn đầy năng lượng, dùng icon sinh động.
2. Kiến thức chuyên sâu: Am hiểu mọi điểm check-in, ẩm thực địa phương (Đà Nẵng, Hội An, Phú Quốc, Đà Lạt, Singapore, Thái Lan, Hàn Quốc...), thời tiết, mẹo đi lại tiết kiệm.
3. Chính sách vé TTLooks: 
   - Mọi vé trên TTLooks là vé điện tử quét mã QR vào cổng trực tiếp, không cần xếp hàng mua vé giấy.
   - Hỗ trợ đổi ngày sử dụng linh hoạt trước 24 giờ hoàn toàn miễn phí tại mục 'Vé đã đặt & E-Ticket'.
4. Trả lời đúng trọng tâm câu hỏi của khách (khoảng 3 - 5 câu), không dài dòng nhưng phải đủ chi tiết hữu ích.";

            var contents = new List<object>();

            // Nạp lịch sử cuộc trò chuyện để AI không bị quên ngữ cảnh
            if (history != null && history.Count > 0)
            {
                foreach (var h in history)
                {
                    contents.Add(new
                    {
                        role = h.Sender == "user" ? "user" : "model",
                        parts = new[] { new { text = h.Text } }
                    });
                }
            }

            // Thêm câu hỏi hiện tại kèm chỉ thị hệ thống
            string finalUserPrompt = contents.Count == 0 
                ? $"{systemInstruction}\n\nKhách hàng hỏi: {currentMessage}" 
                : currentMessage;

            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = finalUserPrompt } }
            });

            string? aiResult = await CallGeminiMultiTurnAsync(contents, 600);
            return !string.IsNullOrWhiteSpace(aiResult) ? aiResult : GenerateSmartFallbackResponse(currentMessage);
        }

        private async Task<string?> CallGeminiMultiTurnAsync(List<object> contents, int maxTokens)
        {
            var requestBody = new
            {
                contents = contents,
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = maxTokens
                }
            };

            string jsonContent = JsonConvert.SerializeObject(requestBody);

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Add("x-goog-api-key", _apiKey);

            foreach (string model in CandidateModels)
            {
                try
                {
                    string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(endpoint, content);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                        var resultJson = JObject.Parse(responseString);
                        var textResult = resultJson["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(textResult))
                        {
                            return textResult.Trim();
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            return null;
        }

        private static string GenerateFallbackPlan(string destination, string days, string budget, string preferences)
        {
            return $"🌟 [KẾ HOẠCH KHÁM PHÁ {destination.ToUpper()} - TỐI ƯU BỞI TTLOOKS]\n" +
                   $"⏱️ Thời lượng: {days} | 💰 Dự toán: {budget} | 🎯 Sở thích: {preferences}\n" +
                   new string('-', 50) + "\n\n" +
                   $"☀️ NGÀY 1: CHECK-IN & THƯỞNG THỨC ĐẶC SẢN\n" +
                   $"• Sáng: Di chuyển đến {destination}, nhận phòng và thưởng thức điểm tâm sáng địa phương.\n" +
                   $"• Trưa: Ăn trưa tại nhà hàng đặc sản nức tiếng.\n" +
                   $"• Chiều: Khám phá các điểm check-in biểu tượng và chụp ảnh kỷ niệm.\n" +
                   $"• Tối: Dạo phố đêm, trải nghiệm ẩm thực đường phố và ngắm cảnh về đêm.\n\n" +
                   $"☀️ NGÀY TIẾP THEO: TRẢI NGHIỆM VUI CHƠI ĐỈNH CAO\n" +
                   $"• Sáng: Tham quan công viên giải trí, đi cáp treo hoặc tham gia tour thuyền.\n" +
                   $"• Trưa: Thưởng thức bữa trưa buffet và nghỉ ngơi.\n" +
                   $"• Chiều: Thư giãn tại quán cafe ngắm cảnh và mua sắm quà lưu niệm.\n\n" +
                   $"💡 MẸO TỪ TTLOOKS: Bạn nên đặt trước vé trên website TTLooks để có mã QR vào cổng ưu tiên không cần xếp hàng nhé!";
        }

        private static string GenerateSmartFallbackResponse(string message)
        {
            string msg = message.ToLower().Trim();

            if (msg.Contains("vé") || msg.Contains("đổi") || msg.Contains("hủy") || msg.Contains("qr"))
                return "Mọi vé trên TTLooks đều là vé điện tử quét mã QR trực tiếp tại cổng soát vé rất nhanh gọn. Bạn có thể đổi ngày tham quan miễn phí trước 24 giờ ngay tại mục 'Vé đã đặt & E-Ticket' nhé! ✨";

            if (msg.Contains("thời tiết") || msg.Contains("mặc gì") || msg.Contains("mùa"))
                return "Thời tiết du lịch hiện tại khá thuận tiện! Bạn nên chuẩn bị đồ năng động, giày thể thao và kem chống nắng. Nếu đi vùng núi cao như Bà Nà Hills hay Đà Lạt thì nhớ mang thêm áo khoác nhẹ vì trên đỉnh nhiệt độ sẽ se lạnh hơn đồng bằng nhé. ⛅";

            if (msg.Contains("ăn gì") || msg.Contains("quán") || msg.Contains("đặc sản"))
                return "Ẩm thực địa phương luôn là phần tuyệt nhất: Đà Nẵng có Mì Quảng & Bánh tráng cuốn thịt heo; Hội An có Cao Lầu & Cơm gà; Đà Lạt có Lẩu gà lá é & bánh tráng nướng. Bạn thích ẩm thực ở thành phố nào để em gợi ý quán chuẩn vị nhất ạ? 🍲";

            return $"Dạ em đây! Về '{message}', em luôn sẵn sàng tư vấn từ mẹo ăn uống, điểm check-in sống ảo đến quy định vé. Bạn cứ chia sẻ thêm chi tiết nhé! ✈️";
        }
    }

    public record ChatTurnDto(string Sender, string Text);
}