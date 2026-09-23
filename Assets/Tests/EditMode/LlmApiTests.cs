using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace InteractVille2.LLM.Tests
{
    [TestFixture, Description("LLM プロキシへのリクエスト本文の組み立て")]
    public class LlmApiRequestTests
    {
        private static List<Message> Messages(params (string role, string content)[] items)
        {
            var list = new List<Message>();
            foreach (var (role, content) in items) list.Add(new Message { role = role, content = content });
            return list;
        }

        [Test, Description("会話リクエストは system と messages（role, content）だけで、model や stream などを含まない")]
        public void ChatRequest_ContainsOnlySystemAndMessages()
        {
            string json = LlmApi.BuildChatRequestJson("あ", Messages(("user", "こんにちは")));

            JObject parsed = JObject.Parse(json);
            Assert.AreEqual(
                JToken.Parse("{\"system\":\"あ\",\"messages\":[{\"role\":\"user\",\"content\":\"こんにちは\"}]}").ToString(),
                parsed.ToString());
        }

        [Test, Description("引用符・バックスラッシュ・改行・制御文字・絵文字などを含む発言も、JSON を壊さずそのまま読み戻せる")]
        public void ChatRequest_EscapesSpecialCharacters()
        {
            const string content = "\"引用\" \\ 改行\n\tタブ \u0000 \u2028 😀 </script> {{x}}";
            string json = LlmApi.BuildChatRequestJson("sys\n\"x\"", Messages(("user", content)));

            JObject parsed = JObject.Parse(json);
            Assert.AreEqual("sys\n\"x\"", (string)parsed["system"]);
            Assert.AreEqual(content, (string)parsed["messages"][0]["content"]);
        }

        [Test, Description("孤立サロゲート（IME の変換途中などで生じる）を含む発言も、送信バイト列では U+FFFD に置き換わるだけで、発言と役割は失われない")]
        public void ChatRequest_LoneSurrogate_BecomesReplacementCharInBytes()
        {
            string content = "あ" + (char)0xD83D;
            string json = LlmApi.BuildChatRequestJson("s", Messages(("user", content), ("assistant", "ok"), ("user", "次")));

            byte[] bytes = LlmApi.ToRequestBytes(json);
            JObject parsed = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes));

            Assert.AreEqual(3, ((JArray)parsed["messages"]).Count);
            Assert.AreEqual("user", (string)parsed["messages"][0]["role"]);
            Assert.AreEqual("あ" + (char)0xFFFD, (string)parsed["messages"][0]["content"]);
            Assert.AreEqual("次", (string)parsed["messages"][2]["content"]);
        }

        [Test, Description("送信バイト列は UTF-8 で、日本語と絵文字をそのまま運ぶ")]
        public void RequestBytes_AreUtf8()
        {
            byte[] bytes = LlmApi.ToRequestBytes("{\"a\":\"あ😀\"}");
            CollectionAssert.AreEqual(
                new byte[] { 0x7B, 0x22, 0x61, 0x22, 0x3A, 0x22, 0xE3, 0x81, 0x82, 0xF0, 0x9F, 0x98, 0x80, 0x22, 0x7D },
                bytes);
        }

        [Test, Description("system・messages・発言の中身が null なら、黙って空文字にせず ArgumentException を投げる")]
        public void ChatRequest_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LlmApi.BuildChatRequestJson(null, Messages(("user", "a"))));
            Assert.Throws<ArgumentNullException>(() => LlmApi.BuildChatRequestJson("s", null));
            Assert.Throws<ArgumentException>(() => LlmApi.BuildChatRequestJson("s", Messages(("user", null))));
            Assert.Throws<ArgumentException>(() => LlmApi.BuildChatRequestJson("s", new List<Message> { null }));
        }

        [Test, Description("スコアのリクエストは prompt だけで、改行や引用符を含む会話ログもそのまま読み戻せる")]
        public void ScoreRequest_ContainsOnlyPrompt()
        {
            const string prompt = "会話データ:user: \"やあ\"\nassistant: こんにちは";
            JObject parsed = JObject.Parse(LlmApi.BuildScoreRequestJson(prompt));

            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual(prompt, (string)parsed["prompt"]);
        }
    }

    [TestFixture, Description("LLM プロキシの応答の解釈")]
    public class LlmApiResponseTests
    {
        [Test, Description("200 の text を会話の本文として受け取る（\\u エスケープも解く）")]
        public void Chat_Success()
        {
            Assert.AreEqual("こんにちは", LlmApi.ParseChatResponse(200, "{\"text\":\"こんにちは\"}", false).Text);
            Assert.AreEqual("こん", LlmApi.ParseChatResponse(200, "{\"text\":\"\\u3053\\u3093\"}", false).Text);
        }

        [Test, Description("日付に見える本文も日付型に変換せず、文字列のまま受け取る")]
        public void Chat_DateLikeText_StaysString()
        {
            LlmResult result = LlmApi.ParseChatResponse(200, "{\"text\":\"2026-09-23T10:00:00\"}", false);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("2026-09-23T10:00:00", result.Text);
        }

        [TestCase("{\"text\":\"\"}")]
        [TestCase("{\"text\":\"  \"}")]
        [TestCase("{\"text\":null}")]
        [TestCase("{\"text\":123}")]
        [TestCase("{}")]
        [TestCase("")]
        [TestCase(null)]
        [Description("200 でも本文が空・欠落・文字列でなければ、空文字の成功にせず Upstream の失敗にする")]
        public void Chat_EmptyOrMissingText_IsUpstream(string body)
        {
            LlmResult result = LlmApi.ParseChatResponse(200, body, false);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(LlmErrorKind.Upstream, result.Error);
        }

        [TestCase("<html>Cloudflare error</html>")]
        [TestCase("{\"text\":\"途中")]
        [TestCase("[]")]
        [TestCase("{\"text\":\"a\"}garbage")]
        [Description("200 でも本文が壊れた JSON や HTML なら、例外を投げず Upstream の失敗にする")]
        public void Chat_BrokenBody_IsUpstream(string body)
        {
            LlmResult result = LlmApi.ParseChatResponse(200, body, false);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(LlmErrorKind.Upstream, result.Error);
        }

        [TestCase(72, 72)]
        [TestCase(0, 0)]
        [TestCase(100, 100)]
        [Description("0〜100 の整数のスコアを受け取る（0 も正しい値として成功扱い）")]
        public void Score_Success(int value, int expected)
        {
            LlmResult result = LlmApi.ParseScoreResponse(200, "{\"result\":" + value + "}", false);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(expected, result.Score);
        }

        [TestCase("{\"result\":null}")]
        [TestCase("{}")]
        [TestCase("{\"result\":\"72\"}")]
        [TestCase("{\"result\":72.5}")]
        [TestCase("{\"result\":-1}")]
        [TestCase("{\"result\":101}")]
        [TestCase("{\"result\":99999999999999999999}")]
        [Description("スコアが欠落・null・文字列・小数・範囲外なら、0 にせず InvalidScore の失敗にする")]
        public void Score_InvalidValue_IsInvalidScore(string body)
        {
            LlmResult result = LlmApi.ParseScoreResponse(200, body, false);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(LlmErrorKind.InvalidScore, result.Error);
        }

        [Test, Description("200 でも本文に error が入っていれば失敗として扱う")]
        public void Status200_WithError_IsFailure()
        {
            Assert.AreEqual(LlmErrorKind.Busy, LlmApi.ParseScoreResponse(200, "{\"error\":\"busy\",\"result\":50}", false).Error);
            Assert.AreEqual(LlmErrorKind.Busy, LlmApi.ParseChatResponse(200, "{\"error\":\"busy\",\"text\":\"a\"}", false).Error);
        }

        [TestCase(429, "{\"error\":\"daily_quota\"}", LlmErrorKind.DailyQuota)]
        [TestCase(503, "{\"error\":\"busy\"}", LlmErrorKind.Busy)]
        [TestCase(502, "{\"error\":\"invalid_score\"}", LlmErrorKind.InvalidScore)]
        [TestCase(400, "{\"error\":\"bad_request\"}", LlmErrorKind.BadRequest)]
        [TestCase(502, "{\"error\":\"upstream\"}", LlmErrorKind.Upstream)]
        [TestCase(500, "{\"error\":\"config\"}", LlmErrorKind.Upstream)]
        [TestCase(500, "{}", LlmErrorKind.Upstream)]
        [Description("プロキシのエラー種別を LlmErrorKind に対応させる")]
        public void ErrorBody_MapsToKind(int status, string body, LlmErrorKind expected)
        {
            Assert.AreEqual(expected, LlmApi.ParseChatResponse(status, body, false).Error);
            Assert.AreEqual(expected, LlmApi.ParseScoreResponse(status, body, false).Error);
        }

        [TestCase(429, "<html>Too Many Requests</html>", LlmErrorKind.DailyQuota)]
        [TestCase(503, "", LlmErrorKind.Busy)]
        [TestCase(400, null, LlmErrorKind.BadRequest)]
        [TestCase(404, "Not Found", LlmErrorKind.Upstream)]
        [TestCase(401, "{\"message\":\"x\"}", LlmErrorKind.Upstream)]
        [TestCase(503, "{\"error\":\"unknown_kind\"}", LlmErrorKind.Busy)]
        [Description("本文からエラー種別を読めないときは HTTP ステータスで判定する（429→DailyQuota、503→Busy、400→BadRequest、他→Upstream）")]
        public void UnreadableErrorBody_UsesStatus(int status, string body, LlmErrorKind expected)
        {
            Assert.AreEqual(expected, LlmApi.ParseChatResponse(status, body, false).Error);
        }

        [Test, Description("接続失敗・タイムアウト（ステータス 0）は、本文にかかわらず Network の失敗にする")]
        public void NetworkError_IsNetwork()
        {
            Assert.AreEqual(LlmErrorKind.Network, LlmApi.ParseChatResponse(0, null, true).Error);
            Assert.AreEqual(LlmErrorKind.Network, LlmApi.ParseChatResponse(0, "", false).Error);
            Assert.AreEqual(LlmErrorKind.Network, LlmApi.ParseScoreResponse(200, "{\"result\":50}", true).Error);
        }
    }
}
