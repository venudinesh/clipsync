// Redacting secrets.
//
// The desktop twin of the phone's redactSecrets tests: tokens, keys, JWTs,
// private key blocks, bearer tokens and password-style assignments are masked
// with labelled markers, a second run finds nothing, and plain prose passes
// through untouched.

using System;

namespace ClipSyncAI.Tests
{
    internal static class SecretsTests
    {
        public static void Run()
        {
            T.Group("Redacting secrets");

            Redaction gh = Secrets.Redact(
                "deploy with ghp_abcdefghijklmnopqrstuvwxyz0123456789AB please");
            T.Eq("a GitHub token is masked", 1, gh.Count);
            T.Contains("with its label", gh.Text, "[redacted:github-token]");
            T.NotContains("and no token bytes survive", gh.Text, "ghp_");

            Redaction ai = Secrets.Redact(
                "a sk-proj-abcdefghij1234567890xyz and sk-ant-abcdefghij1234567890");
            T.Eq("an OpenAI and an Anthropic key are both caught", 2, ai.Count);
            T.Contains("the OpenAI one", ai.Text, "[redacted:openai-key]");
            T.Contains("the Anthropic one", ai.Text, "[redacted:anthropic-key]");

            Redaction aws = Secrets.Redact("key AKIAIOSFODNN7EXAMPLE here");
            T.Eq("an AWS access key is masked", 1, aws.Count);
            T.Contains("with its label", aws.Text, "[redacted:aws-access-key]");

            Redaction three = Secrets.Redact(
                "g AIzaSyDabcdefghijklmnopqrstuvw01234567890 and " +
                "x xoxb-123456789012-abcdefghij plus sk_live_abcdefghijklmnop ok");
            T.Eq("a Google key, a Slack token and a Stripe secret", 3, three.Count);
            T.Contains("the Google one", three.Text, "[redacted:google-api-key]");
            T.Contains("the Slack one", three.Text, "[redacted:slack-token]");
            T.Contains("the Stripe one", three.Text, "[redacted:stripe-secret-key]");

            Redaction jwt = Secrets.Redact(
                "token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dummy-signature-part-here12.");
            T.Eq("a JWT is masked", 1, jwt.Count);
            T.Eq("but the sentence punctuation stays",
                "token [redacted:jwt].", jwt.Text);

            Redaction pem = Secrets.Redact("cert:\n" +
                "-----BEGIN RSA PRIVATE KEY-----\n" +
                "MIIEpAIBAAKCAQEA7b\n" +
                "-----END RSA PRIVATE KEY-----\ndone");
            T.Eq("a private key block is masked whole", 1, pem.Count);
            T.Eq("fences included",
                "cert:\n[redacted:private-key]\ndone", pem.Text);

            Redaction bearer = Secrets.Redact("Authorization: Bearer abcdef1234567890");
            T.Eq("a bearer token is masked", 1, bearer.Count);
            T.Contains("with its label", bearer.Text, "[redacted:bearer-token]");

            Redaction card = Secrets.Redact("card 4111111111111111 expires soon");
            T.Eq("a card number passing Luhn is masked", 1, card.Count);
            T.Contains("with its label", card.Text, "[redacted:card-number]");

            Redaction order = Secrets.Redact("order 4111111111111112 shipped");
            T.Eq("a number failing Luhn is kept", 0, order.Count);
            T.Contains("untouched", order.Text, "4111111111111112");

            Redaction assigned = Secrets.Redact(
                "db password=hunter2\nenv API_KEY=\"abc123XYZ\"\nclientSecret: s3cr3t!");
            T.Eq("a password, an api key and a client secret", 3, assigned.Count);
            T.Contains("keeping the password label",
                assigned.Text, "password=[redacted:password]");
            T.Contains("keeping the key label and quotes",
                assigned.Text, "API_KEY=\"[redacted:api_key]\"");
            T.Contains("keeping the camelCase label",
                assigned.Text, "clientSecret=[redacted:clientsecret]");
            T.NotContains("and no secret bytes survive",
                assigned.Text, "hunter2");

            Redaction once = Secrets.Redact("key sk-abcdefghij1234567890xyz and pwd=opensesame");
            T.Eq("the first run catches", true, once.Count > 0);
            Redaction twice = Secrets.Redact(once.Text);
            T.Eq("a second run is a no-op", 0, twice.Count);
            T.Eq("and the text stands", once.Text, twice.Text);

            const string prose =
                "My password is safe with me. Take a token of my love. Ring the bell.";
            Redaction plain = Secrets.Redact(prose);
            T.Eq("plain prose holds no secrets", 0, plain.Count);
            T.Eq("and passes through untouched", prose, plain.Text);
        }
    }
}
