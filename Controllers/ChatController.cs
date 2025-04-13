using EmailChatASP.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace EmailChatASP.Controllers
{
    public class ChatController : Controller
    {
        private readonly ChatService _chatService;

        public ChatController(ChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost]
        public async Task<IActionResult> GetResponse([FromBody] ChatRequest chatRequest)
        {
            // Gọi service để lấy phản hồi từ chatbot
            var aiResponse = await _chatService.GetResponse(chatRequest);

            // Trả về kết quả trong dạng JSON
            return Json(new { response = aiResponse });
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; }
    }
}
