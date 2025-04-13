using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Net.Imap;  
using MailKit.Security;  
using MailKit;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmailChatASP.Models;

namespace EmailChatASP.Services
{
    public class EmailService
    {
        //smtp 
        private readonly string _smtpServer = "smtp.gmail.com";
        private readonly int _smtpPort = 587;
        private readonly string _email = "thanhhh2005@gmail.com"; // Địa chỉ email của bạn
        private readonly string _appPassword = "wzkqturmycclizro";  // Mật khẩu ứng dụng Gmail

        //imap
        private readonly string _imapServer = "imap.gmail.com";  // Máy chủ IMAP của Gmail
        private readonly int _imapPort = 993;  // Cổng IMAP SSL của Gmail
        

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Your Name", _email)); // Địa chỉ email người gửi
            message.To.Add(new MailboxAddress("Reciper", toEmail)); // Địa chỉ email người nhận
            message.Subject = subject;

            var bodyPart = new TextPart("plain")
            {
                Text = body
            };

            message.Body = bodyPart;

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);  // Kết nối tới SMTP Gmail
                await client.AuthenticateAsync(_email, _appPassword);  // Xác thực tài khoản
                await client.SendAsync(message);  // Gửi email
                await client.DisconnectAsync(true);  // Ngắt kết nối sau khi gửi
            }
        }
        public async Task<List<MimeMessage>> ReadEmailsAsync(int pageSize, int pageNumber)
        {
            var emails = new List<MimeMessage>();
            try
            {
                using (var client = new ImapClient())
                {
                    await client.ConnectAsync(_imapServer, _imapPort, true);  // Kết nối với Gmail qua SSL
                    await client.AuthenticateAsync(_email, _appPassword);  // Đăng nhập với mật khẩu ứng dụng

                    var inbox = client.Inbox;
                    await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly);  // Mở thư mục Inbox chỉ để đọc

                    var messageCount = inbox.Count;

                    // Tính toán các chỉ số cho phân trang
                    int skipCount = (pageNumber - 1) * pageSize;
                    int fetchCount = Math.Min(pageSize, messageCount - skipCount);

                    for (int i = messageCount - skipCount - 1; i >= messageCount - skipCount - fetchCount; i--)
                    {
                        var message = await inbox.GetMessageAsync(i);  // Lấy email theo index
                        emails.Add(message);
                    }

                    await client.DisconnectAsync(true);  // Ngắt kết nối
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error reading emails: " + ex.Message);
            }

            return emails;
        }
        public async Task<MimeMessage> GetEmailByIdAsync(string messageId)
        {
            using (var client = new ImapClient())
            {
                await client.ConnectAsync(_imapServer, _imapPort, true);
                await client.AuthenticateAsync(_email, _appPassword);

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);

                for (int i = inbox.Count - 1; i >= 0; i--)
                {
                    var message = await inbox.GetMessageAsync(i);
                    if (message.MessageId == messageId)
                    {
                        await client.DisconnectAsync(true);
                        return message; // Trả về email khớp với messageId
                    }
                }

                await client.DisconnectAsync(true);
                return null; // Không tìm thấy email
            }
        }
    }
}
