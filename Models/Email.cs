namespace EmailChatASP.Models
{
    public class Email
    {
        public int Id { get; set; }
        public string Sender { get; set; }
        public string Subject { get; set; }
        public DateTime DateReceived { get; set; }
        public string RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
