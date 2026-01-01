using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Communication.Email;
using VANTAGE.Utilities;

namespace VANTAGE.Utilities
{
    public static class EmailService
    {
        private static EmailClient? _client;

        // Initialize the email client (lazy initialization)
        private static EmailClient GetClient()
        {
            if (_client == null)
            {
                _client = new EmailClient(Credentials.AzureEmailConnectionString);
            }
            return _client;
        }

        // Send assignment notification email
        // Returns true if sent successfully, false otherwise (does not throw)
        public static async Task<bool> SendAssignmentNotificationAsync(
            string recipientEmail,
            string recipientName,
            string assignedByUsername,
            int recordCount,
            List<string> projectIds)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(recipientEmail))
                {
                    AppLogger.Warning("Cannot send assignment email - recipient email is empty",
                        "EmailService.SendAssignmentNotificationAsync");
                    return false;
                }

                var client = GetClient();

                string projectList = projectIds.Count > 0
                    ? string.Join(", ", projectIds.Distinct())
                    : "Unknown";

                string subject = $"MILESTONE: {recordCount} record(s) assigned to you";

                string htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0078D7; color: white; padding: 15px 20px; border-radius: 4px 4px 0 0; }}
        .content {{ background-color: #f5f5f5; padding: 20px; border-radius: 0 0 4px 4px; }}
        .highlight {{ font-size: 24px; font-weight: bold; color: #0078D7; }}
        .details {{ margin-top: 15px; }}
        .detail-row {{ padding: 8px 0; border-bottom: 1px solid #ddd; }}
        .label {{ font-weight: 600; color: #555; }}
        .footer {{ margin-top: 20px; font-size: 12px; color: #888; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2 style='margin: 0;'>MILESTONE Assignment Notification</h2>
        </div>
        <div class='content'>
            <p>Hello {recipientName},</p>
            <p>You have been assigned new records in MILESTONE:</p>
            <p class='highlight'>{recordCount} record(s)</p>
            <div class='details'>
                <div class='detail-row'>
                    <span class='label'>Assigned by:</span> {assignedByUsername}
                </div>
                <div class='detail-row'>
                    <span class='label'>Project(s):</span> {projectList}
                </div>
                <div class='detail-row'>
                    <span class='label'>Date:</span> {DateTime.Now:MMMM d, yyyy h:mm tt}
                </div>
            </div>
            <p style='margin-top: 20px;'>Please open MILESTONE and SYNC now to see your records.</p>
            <div class='footer'>
                <p>This is an automated message from MILESTONE. Please do not reply to this email.</p>
            </div>
        </div>
    </div>
</body>
</html>";

                string plainTextBody = $@"MILESTONE Assignment Notification

Hello {recipientName},

You have been assigned {recordCount} record(s) in MILESTONE.

Assigned by: {assignedByUsername}
Project(s): {projectList}
Date: {DateTime.Now:MMMM d, yyyy h:mm tt}

Please open MILESTONE and SYNC now to see your records.

This is an automated message from MILESTONE. Please do not reply to this email.";

                var emailMessage = new EmailMessage(
                    senderAddress: Credentials.AzureEmailSenderAddress,
                    recipientAddress: recipientEmail,
                    content: new EmailContent(subject)
                    {
                        Html = htmlBody,
                        PlainText = plainTextBody
                    });

                EmailSendOperation emailSendOperation = await client.SendAsync(
                    WaitUntil.Started,
                    emailMessage);

                AppLogger.Info(
                    $"Assignment email sent to {recipientEmail} ({recordCount} records from {assignedByUsername})",
                    "EmailService.SendAssignmentNotificationAsync",
                    assignedByUsername);

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "EmailService.SendAssignmentNotificationAsync");
                return false;
            }
        }

        // Generic send email method for future use
        public static async Task<bool> SendEmailAsync(
            string recipientEmail,
            string subject,
            string htmlBody,
            string? plainTextBody = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(recipientEmail))
                {
                    AppLogger.Warning("Cannot send email - recipient email is empty",
                        "EmailService.SendEmailAsync");
                    return false;
                }

                var client = GetClient();

                var content = new EmailContent(subject) { Html = htmlBody };
                if (!string.IsNullOrWhiteSpace(plainTextBody))
                {
                    content.PlainText = plainTextBody;
                }

                var emailMessage = new EmailMessage(
                    senderAddress: Credentials.AzureEmailSenderAddress,
                    recipientAddress: recipientEmail,
                    content: content);

                await client.SendAsync(WaitUntil.Started, emailMessage);

                AppLogger.Info($"Email sent to {recipientEmail}: {subject}", "EmailService.SendEmailAsync");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "EmailService.SendEmailAsync");
                return false;
            }
        }

        // Send email with file attachment
        public static async Task<bool> SendEmailWithAttachmentAsync(
            string recipientEmail,
            string recipientName,
            string subject,
            string htmlBody,
            string attachmentName,
            byte[] attachmentData,
            string contentType = "text/plain")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(recipientEmail))
                {
                    AppLogger.Warning("Cannot send email - recipient email is empty",
                        "EmailService.SendEmailWithAttachmentAsync");
                    return false;
                }

                var client = GetClient();

                var emailMessage = new EmailMessage(
                    senderAddress: Credentials.AzureEmailSenderAddress,
                    recipientAddress: recipientEmail,
                    content: new EmailContent(subject) { Html = htmlBody });

                // Add attachment
                var attachment = new EmailAttachment(attachmentName, contentType, BinaryData.FromBytes(attachmentData));
                emailMessage.Attachments.Add(attachment);

                await client.SendAsync(WaitUntil.Started, emailMessage);

                AppLogger.Info($"Email with attachment sent to {recipientEmail}: {subject}",
                    "EmailService.SendEmailWithAttachmentAsync", App.CurrentUser?.Username ?? "Unknown");

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "EmailService.SendEmailWithAttachmentAsync");
                return false;
            }
        }
    }
}