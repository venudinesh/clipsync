// Clips: redacting secrets.
//
// The desktop twin of the phone's redactSecrets: API keys, tokens, passwords
// and private keys are replaced with labelled [redacted:kind] markers, and the
// run reports how many it caught. Pure regex, no model, so it works the same
// offline on every platform — and a second run is a no-op, because a marker is
// never a secret worth masking again.

using System;
using System.Text.RegularExpressions;

namespace ClipSyncAI
{
    internal struct Redaction
    {
        public string Text;
        public int Count;
    }

    internal static class Secrets
    {
        private struct Fixed
        {
            public string Pattern;
            public string Label;
            public Fixed(string pattern, string label)
            {
                Pattern = pattern;
                Label = label;
            }
        }

        private static readonly Fixed[] Shapes = new Fixed[]
        {
            new Fixed(@"sk-ant-[A-Za-z0-9_-]{10,}", "anthropic-key"),
            new Fixed(@"sk-proj-[A-Za-z0-9_-]{20,}", "openai-key"),
            new Fixed(@"sk-[A-Za-z0-9_-]{20,}", "openai-key"),
            new Fixed(@"(?:gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{20,})", "github-token"),
            new Fixed(@"AKIA[0-9A-Z]{16}", "aws-access-key"),
            new Fixed(@"AIza[0-9A-Za-z\-_]{35}", "google-api-key"),
            new Fixed(@"xox[bpas]-[A-Za-z0-9-]{10,}", "slack-token"),
            new Fixed(@"(?:sk_live|rk_live)_[A-Za-z0-9]{16,}", "stripe-secret-key"),
            new Fixed(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", "bearer-token"),
        };

        private static readonly Regex Jwt = new Regex(
            @"eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}");

        private static readonly Regex Pem = new Regex(
            @"-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z0-9 ]*PRIVATE KEY-----");

        private static readonly Regex Assigned = new Regex(
            @"([A-Za-z0-9_.\-]*?(?:api[_-]?key|secret|passwd|password|pwd|auth[_-]?token|access[_-]?token|client[_-]?secret)[A-Za-z0-9_.\-]*)\s*[:=]\s*([""']?)([^\s""'`,;]+)\2",
            RegexOptions.IgnoreCase);

        /// Sentence punctuation hanging off a token is not part of it.
        private static readonly char[] JwtTail = new char[]
            { '.', ',', ';', ':', '!', '?', ')', ']', '}', '"', '\'' };

        public static Redaction Redact(string text)
        {
            string s = text ?? "";
            int n = 0;
            for (int i = 0; i < Shapes.Length; i++)
            {
                string label = Shapes[i].Label;
                s = Regex.Replace(s, Shapes[i].Pattern, delegate(Match m)
                {
                    n++;
                    return "[redacted:" + label + "]";
                });
            }
            s = Jwt.Replace(s, delegate(Match m)
            {
                string token = m.Value;
                string tail = "";
                while (token.Length > 0 &&
                    Array.IndexOf(JwtTail, token[token.Length - 1]) >= 0)
                {
                    tail = token[token.Length - 1] + tail;
                    token = token.Substring(0, token.Length - 1);
                }
                n++;
                return "[redacted:jwt]" + tail;
            });
            s = Pem.Replace(s, delegate(Match m)
            {
                n++;
                return "[redacted:private-key]";
            });
            s = Assigned.Replace(s, delegate(Match m)
            {
                string key = m.Groups[1].Value;
                string quote = m.Groups[2].Value;
                string value = m.Groups[3].Value;
                if (value.StartsWith("[redacted:", StringComparison.Ordinal))
                    return m.Value;
                n++;
                return key + "=" + quote +
                    "[redacted:" + key.ToLowerInvariant() + "]" + quote;
            });
            Redaction r = new Redaction();
            r.Text = s;
            r.Count = n;
            return r;
        }
    }
}
