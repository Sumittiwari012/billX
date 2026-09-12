namespace MyWPFCRUDApp.Services
{
    // ════════════════════════════════════════════════════════════════════════
    // SmtpHostLookup — guesses the outgoing mail server from an email
    // address's domain, so the Backup Settings screen doesn't need a manual
    // "SMTP host" box for the handful of providers people actually use.
    // ════════════════════════════════════════════════════════════════════════
    public static class SmtpHostLookup
    {
        public static string Detect(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                return string.Empty;

            string domain = email[(email.IndexOf('@') + 1)..].Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(domain))
                return string.Empty;

            return domain switch
            {
                "gmail.com" or "googlemail.com" => "smtp.gmail.com",
                "outlook.com" or "hotmail.com" or "live.com" or "msn.com" => "smtp.office365.com",
                "yahoo.com" or "yahoo.co.in" or "ymail.com" => "smtp.mail.yahoo.com",
                "icloud.com" or "me.com" or "mac.com" => "smtp.mail.me.com",
                "zoho.com" or "zohomail.com" or "zoho.in" => "smtp.zoho.com",
                "yandex.com" or "yandex.ru" => "smtp.yandex.com",
                "rediffmail.com" => "smtp.rediffmail.com",

                // Unrecognised domain — almost always a custom/company email
                // hosted through shared hosting or a control panel like
                // cPanel, where "smtp.<domain>" is the single most common
                // convention. It's the best default guess available without
                // asking the user to type one in.
                _ => $"smtp.{domain}"
            };
        }
    }
}