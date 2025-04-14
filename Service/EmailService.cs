using MimeKit;
using MailKit.Search; 
using System.Linq;    
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

        //Ilogger
        private readonly ILogger<EmailService> _logger;

        public EmailService(ILogger<EmailService> logger) 
        {
            _logger = logger;   
        }

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
                    await client.ConnectAsync(_imapServer, _imapPort, SecureSocketOptions.SslOnConnect);
                    await client.AuthenticateAsync(_email, _appPassword);

                    // --- THAY ĐỔI QUAN TRỌNG: Mở thư mục "[Gmail]/All Mail" ---
                    // Lấy đối tượng thư mục All Mail (Tên có thể khác nếu giao diện Gmail không phải tiếng Anh)
                    var allMailFolder = client.GetFolder(SpecialFolder.All); // MailKit cung cấp cách lấy các thư mục đặc biệt
                    if (allMailFolder == null)
                    {
                        // Dự phòng nếu SpecialFolder.All không hoạt động (thử tên thủ công)
                        try
                        {
                            allMailFolder = client.GetFolder("[Gmail]/All Mail"); // Thử tên tiếng Anh phổ biến
                        }
                        catch (FolderNotFoundException)
                        {
                            _logger.LogError("Could not find '[Gmail]/All Mail' folder.");
                            // Có thể thử các tên khác tùy ngôn ngữ Gmail hoặc ném lỗi
                            throw new Exception("Could not access the All Mail folder. Please check folder name in Gmail settings.");
                        }
                    }

                    await allMailFolder.OpenAsync(FolderAccess.ReadOnly);
                    _logger.LogInformation("Successfully opened folder: {FolderName}", allMailFolder.FullName);

                    // --- Phần tìm kiếm và fetch giữ nguyên logic UID ---

                    // 1. Tìm kiếm UID trong All Mail
                    var searchQuery = SearchQuery.All; // Lấy tất cả trong All Mail
                                                       // Cân nhắc lọc theo ngày để giới hạn kết quả nếu All Mail quá lớn
                                                       // var searchQuery = SearchQuery.DeliveredAfter(DateTime.Now.AddDays(-60));

                    var uids = await allMailFolder.SearchAsync(searchQuery);
                    _logger.LogInformation("Search in '{FolderName}' found {UidCount} UIDs.", allMailFolder.FullName, uids.Count);


                    // 2. Sắp xếp UIDs theo thứ tự giảm dần (mới nhất trước)
                    var sortedUids = uids.Reverse().ToList();

                    // 3. Áp dụng logic phân trang trên danh sách UIDs
                    int skipCount = (pageNumber - 1) * pageSize;
                    var uidsForPage = sortedUids.Skip(skipCount).Take(pageSize).ToList();

                    _logger.LogInformation("Fetching page {PageNumber} ({PageSize} emails) with UIDs: {Uids}",
                                          pageNumber, uidsForPage.Count, string.Join(",", uidsForPage));

                    // 4. Lặp qua UIDs của trang hiện tại và lấy nội dung email
                    foreach (var uid in uidsForPage)
                    {
                        try
                        {
                            // Sử dụng allMailFolder thay vì inbox
                            var message = await allMailFolder.GetMessageAsync(uid);
                            if (message != null)
                            {
                                emails.Add(message);
                            }
                            else
                            {
                                _logger.LogWarning("Could not fetch message with UID: {Uid} from {FolderName}", uid, allMailFolder.FullName);
                            }
                        }
                        catch (Exception msgEx)
                        {
                            _logger.LogError(msgEx, "Error fetching individual email with UID: {Uid} from {FolderName}", uid, allMailFolder.FullName);
                        }
                    }

                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading emails overall from All Mail. PageNumber: {PageNumber}, PageSize: {PageSize}", pageNumber, pageSize);
                throw new Exception("Error reading emails: " + ex.Message, ex);
            }

            // Quan trọng: Sắp xếp lại danh sách emails theo ngày giảm dần trước khi trả về
            // vì thứ tự fetch từ All Mail có thể không hoàn toàn theo thời gian như mong đợi
            return emails.OrderByDescending(m => m.Date).ToList();
        }
        public async Task<MimeMessage> GetEmailByIdAsync(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId)) return null;
            string searchMessageId = messageId.Trim('<', '>');

            try
            {
                using (var client = new ImapClient())
                {
                    await client.ConnectAsync(_imapServer, _imapPort, SecureSocketOptions.SslOnConnect);
                    await client.AuthenticateAsync(_email, _appPassword);

                    IMailFolder targetFolder = null;
                    UniqueId? foundUid = null;

                    // Ưu tiên tìm trong Inbox trước
                    var inbox = client.Inbox;
                    await inbox.OpenAsync(FolderAccess.ReadOnly);
                    var query = SearchQuery.HeaderContains("Message-ID", searchMessageId);
                    var uidsInbox = await inbox.SearchAsync(query);

                    if (uidsInbox.Count > 0)
                    {
                        targetFolder = inbox;
                        foundUid = uidsInbox[0];
                        _logger.LogInformation("Found email with Message-ID '{MessageId}' in Inbox, UID {Uid}", messageId, foundUid);
                    }
                    else
                    {
                        _logger.LogWarning("Email with Message-ID '{MessageId}' not found in Inbox, trying All Mail.", messageId);
                        // Nếu không thấy trong Inbox, thử tìm trong All Mail
                        var allMailFolder = client.GetFolder(SpecialFolder.All);
                        if (allMailFolder != null)
                        {
                            try
                            {
                                await allMailFolder.OpenAsync(FolderAccess.ReadOnly);
                                var uidsAllMail = await allMailFolder.SearchAsync(query);
                                if (uidsAllMail.Count > 0)
                                {
                                    targetFolder = allMailFolder;
                                    foundUid = uidsAllMail[0];
                                    _logger.LogInformation("Found email with Message-ID '{MessageId}' in All Mail, UID {Uid}", messageId, foundUid);
                                }
                                else
                                {
                                    _logger.LogWarning("Email with Message-ID '{MessageId}' not found in All Mail either.", messageId);
                                }
                            }
                            catch (Exception folderEx)
                            {
                                _logger.LogError(folderEx, "Error accessing or searching All Mail folder for Message-ID: {MessageId}", messageId);
                            }
                        }
                    }


                    if (targetFolder != null && foundUid.HasValue)
                    {
                        var message = await targetFolder.GetMessageAsync(foundUid.Value);
                        await client.DisconnectAsync(true);
                        return message;
                    }

                    await client.DisconnectAsync(true);
                    return null; // Không tìm thấy ở đâu cả
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting email by Message-ID: {MessageId}", messageId);
                return null;
            }
        }
    }
}
