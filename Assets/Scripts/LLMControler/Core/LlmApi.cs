using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InteractVille2.LLM
{
    /// <summary>
    /// LLM プロキシ（server/llm-proxy）の API。リクエスト本文の組み立てと、応答の解釈を行う。
    ///
    ///   POST /chat   { system, messages } → 200 { text }
    ///   POST /score  { prompt }           → 200 { result }（0〜100 の整数）
    ///   失敗                               → 4xx/5xx { error: 種別 }
    ///
    /// 形式は server/llm-proxy/contract/examples.json と一致させる（EditMode テストで確認している）。
    /// JsonUtility は欠けた項目を 0 や空文字にしてしまい、「応答なし」を「親密度 0」と取り違えるので、
    /// 項目の有無を判定できる Newtonsoft の JObject で読む。
    /// </summary>
    public static class LlmApi
    {
        public const string ChatPath = "/chat";
        public const string ScorePath = "/score";

        public static string BuildChatRequestJson(string system, IReadOnlyList<Message> messages)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (messages == null) throw new ArgumentNullException(nameof(messages));

            var array = new JArray();
            foreach (var message in messages)
            {
                if (message == null || message.content == null || message.role == null)
                {
                    throw new ArgumentException("role と content が null のメッセージは送れません。", nameof(messages));
                }
                array.Add(new JObject { ["role"] = message.role, ["content"] = message.content });
            }
            return new JObject { ["system"] = system, ["messages"] = array }.ToString(Formatting.None);
        }

        /// <summary>
        /// 送信するバイト列（UTF-8）。IME の変換途中などで生じた孤立サロゲートは U+FFFD に置き換わる
        /// （.NET の UTF8Encoding の既定の動作）。例外にはならず、発言そのものは失われない。
        /// </summary>
        public static byte[] ToRequestBytes(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            return Encoding.UTF8.GetBytes(json);
        }

        public static string BuildScoreRequestJson(string prompt)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            return new JObject { ["prompt"] = prompt }.ToString(Formatting.None);
        }

        /// <summary>/chat の応答を解釈する。networkError は UnityWebRequest の接続失敗・タイムアウト。</summary>
        public static LlmResult ParseChatResponse(long status, string body, bool networkError)
        {
            if (TryGetFailure(status, body, networkError, out var failure, out var json)) return failure;

            var text = json?["text"];
            if (text == null || text.Type != JTokenType.String) return LlmResult.Fail(LlmErrorKind.Upstream);
            var value = text.Value<string>();
            return string.IsNullOrWhiteSpace(value) ? LlmResult.Fail(LlmErrorKind.Upstream) : LlmResult.FromText(value);
        }

        /// <summary>/score の応答を解釈する。0〜100 の整数以外は InvalidScore（0 として扱わない）。</summary>
        public static LlmResult ParseScoreResponse(long status, string body, bool networkError)
        {
            if (TryGetFailure(status, body, networkError, out var failure, out var json)) return failure;

            var result = json?["result"];
            if (result == null || result.Type != JTokenType.Integer) return LlmResult.Fail(LlmErrorKind.InvalidScore);
            long score;
            try
            {
                score = result.Value<long>();
            }
            catch (Exception e) when (e is OverflowException || e is InvalidCastException)
            {
                // long に収まらない巨大な整数（BigInteger として読まれる）
                return LlmResult.Fail(LlmErrorKind.InvalidScore);
            }
            if (score < 0 || score > 100) return LlmResult.Fail(LlmErrorKind.InvalidScore);
            return LlmResult.FromScore((int)score);
        }

        // 失敗かどうかを判定する。成功（200 で error 項目なし）のときは本文の JSON を返す。
        private static bool TryGetFailure(long status, string body, bool networkError, out LlmResult failure, out JObject json)
        {
            json = null;
            if (networkError || status == 0)
            {
                failure = LlmResult.Fail(LlmErrorKind.Network);
                return true;
            }

            json = TryParseObject(body);
            var error = json?["error"];
            if (error != null && error.Type == JTokenType.String)
            {
                failure = LlmResult.Fail(ToErrorKind(error.Value<string>(), status));
                return true;
            }
            if (status != 200)
            {
                failure = LlmResult.Fail(FromStatus(status));
                return true;
            }
            if (json == null)
            {
                failure = LlmResult.Fail(LlmErrorKind.Upstream);
                return true;
            }

            failure = null;
            return false;
        }

        private static JObject TryParseObject(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            // 既定の DateParseHandling では、日付に見える台詞（"2026-09-23T10:00:00" など）が
            // 日付型に変わり、文字列として読めなくなる。
            using (var reader = new JsonTextReader(new StringReader(body)) { DateParseHandling = DateParseHandling.None })
            {
                try
                {
                    var token = JToken.ReadFrom(reader);
                    // 末尾に余計な内容が続く本文（"{}garbage"）は壊れているとみなす。
                    if (reader.Read()) return null;
                    return token as JObject;
                }
                catch (JsonException)
                {
                    return null;
                }
            }
        }

        private static LlmErrorKind ToErrorKind(string error, long status)
        {
            switch (error)
            {
                case "daily_quota": return LlmErrorKind.DailyQuota;
                case "busy": return LlmErrorKind.Busy;
                case "invalid_score": return LlmErrorKind.InvalidScore;
                case "bad_request": return LlmErrorKind.BadRequest;
                case "upstream":
                case "config":
                    return LlmErrorKind.Upstream;
                default:
                    return status == 200 ? LlmErrorKind.Upstream : FromStatus(status);
            }
        }

        // 本文から種別を読めないときは、ステータスで判定する。
        private static LlmErrorKind FromStatus(long status)
        {
            if (status == 429) return LlmErrorKind.DailyQuota;
            if (status == 503) return LlmErrorKind.Busy;
            if (status == 400) return LlmErrorKind.BadRequest;
            return LlmErrorKind.Upstream;
        }
    }
}
