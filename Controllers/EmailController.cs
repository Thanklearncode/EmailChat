using EmailChatASP.Models;
using EmailChatASP.Services;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using System.Collections.Generic;
using System.Threading.Tasks;

public class EmailController : Controller
{
    private readonly EmailService _emailService;

    public EmailController(EmailService emailService)
    {
        _emailService = emailService;
    }

    // Action Soạn email mới
    [HttpGet]
    public IActionResult Compose()
    {
        return View();
    }

    // Action gửi email
    [HttpPost]
    public async Task<IActionResult> Compose(EmailRequest emailRequest)
    {
        if (emailRequest == null || string.IsNullOrEmpty(emailRequest.ToEmail) || string.IsNullOrEmpty(emailRequest.Subject) || string.IsNullOrEmpty(emailRequest.Body))
        {
            TempData["ErrorMessage"] = "Email request is incomplete.";
            return RedirectToAction("Compose", "Email");
        }

        try
        {
            await _emailService.SendEmailAsync(emailRequest.ToEmail, emailRequest.Subject, emailRequest.Body);
            TempData["SuccessMessage"] = "Email sent successfully!";
            return RedirectToAction("Inbox", "Email");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Error sending email: " + ex.Message;
            return RedirectToAction("Compose", "Email");
        }
    }


    // Action Inbox
    public async Task<IActionResult> Inbox()
    {
        try
        {
            // Đảm bảo phương thức đọc email không bị treo
            var emails = await _emailService.ReadEmailsAsync(10, 1);
            return View(emails);  // Trả về view với danh sách email
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Error fetching emails: " + ex.Message;
            return View();  // Quay lại view với thông báo lỗi nếu có
        }
    }


    // Action Read email
    public async Task<IActionResult> ReadEmail(string messageId)
    {
        try
        {
            var email = await _emailService.GetEmailByIdAsync(messageId);  // Lấy chi tiết email
            if (email != null)
            {
                return View(email);  // Hiển thị email chi tiết
            }
            else
            {
                TempData["ErrorMessage"] = "Email not found.";
                return RedirectToAction("Inbox", "Email");
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Error reading email: " + ex.Message;
            return RedirectToAction("Inbox", "Email");
        }
    }

}
