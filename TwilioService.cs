using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace VirtualHealthAPI
{
    public class TwilioService
    {
        private readonly string _accountSid;
        private readonly string _authToken;
        private readonly string _fromPhone;

        public TwilioService(IConfiguration config)
        {
            _accountSid = config["Twilio:AccountSID"];
            _authToken = config["Twilio:AuthToken"];
            _fromPhone = config["Twilio:FromPhoneNumber"];
            TwilioClient.Init(_accountSid, _authToken);
        }

        public async Task<bool> SendSmsAsync(string toPhone, string message)
        {
            MessageResource? result = null;
            try {

                var messageOptions = new CreateMessageOptions(
                 new PhoneNumber($"whatsapp:{toPhone}"));
                messageOptions.From = new PhoneNumber($"whatsapp:{_fromPhone}");
                messageOptions.Body = message;


                var messages = MessageResource.Create(messageOptions);
                
                Console.WriteLine($"Sent SID: {messages.Sid} and message {messages.Body}");
            }
            catch (Twilio.Exceptions.AuthenticationException ex)
            {
                Console.WriteLine("Authentication failed: " + ex.Message);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                throw new InvalidOperationException("Failed to send SMS", ex);
            }
            return (result == null); 
        }
    }
}
