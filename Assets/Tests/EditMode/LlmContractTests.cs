using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace InteractVille2.LLM.Tests
{
    [TestFixture, Description("Unity と LLM プロキシ（server/llm-proxy/contract）の契約")]
    public class LlmContractTests
    {
        private static JObject ReadContract(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "..", "server", "llm-proxy", "contract", fileName);
            Assert.IsTrue(File.Exists(path), $"契約ファイルがありません: {path}");
            return JObject.Parse(File.ReadAllText(path));
        }

        [Test, Description("Unity 側で守っている上限（履歴の件数・合計文字数、プロンプトの文字数）は contract/limits.json（Worker と共有）と一致する")]
        public void Limits_MatchWorker()
        {
            JObject limits = ReadContract("limits.json");

            Assert.AreEqual((int)limits["messagesMaxCount"], LlmLimits.MessagesMaxCount);
            Assert.AreEqual((int)limits["messagesMaxTotalChars"], LlmLimits.MessagesMaxTotalChars);
            Assert.AreEqual((int)limits["promptMaxChars"], LlmLimits.PromptMaxChars);
        }

        [Test, Description("組み立てた会話リクエストは、契約の例と同じ形になる")]
        public void ChatRequest_MatchesContract()
        {
            JObject example = (JObject)ReadContract("examples.json")["chatRequest"];
            var messages = new List<Message>();
            foreach (JToken m in example["messages"]) messages.Add(new Message { role = (string)m["role"], content = (string)m["content"] });

            JObject built = JObject.Parse(LlmApi.BuildChatRequestJson((string)example["system"], messages));

            Assert.IsTrue(JToken.DeepEquals(example, built), built.ToString());
        }

        [Test, Description("組み立てたスコアのリクエストは、契約の例と同じ形になる")]
        public void ScoreRequest_MatchesContract()
        {
            JObject example = (JObject)ReadContract("examples.json")["scoreRequest"];

            JObject built = JObject.Parse(LlmApi.BuildScoreRequestJson((string)example["prompt"]));

            Assert.IsTrue(JToken.DeepEquals(example, built), built.ToString());
        }

        [Test, Description("契約の成功応答を、会話の本文・スコアとして読める")]
        public void SuccessResponses_AreParsed()
        {
            JObject examples = ReadContract("examples.json");
            JToken chat = examples["chatSuccess"], score = examples["scoreSuccess"];

            LlmResult chatResult = LlmApi.ParseChatResponse((long)chat["status"], chat["body"].ToString(), false);
            LlmResult scoreResult = LlmApi.ParseScoreResponse((long)score["status"], score["body"].ToString(), false);

            Assert.AreEqual((string)chat["body"]["text"], chatResult.Text);
            Assert.AreEqual((int)score["body"]["result"], scoreResult.Score);
        }

        [Test, Description("契約の成功応答を別のエンドポイントの形として読んだら失敗になる（会話の本文をスコアとして、スコアを本文として受け取らない）")]
        public void SuccessResponses_AreNotConfused()
        {
            JObject examples = ReadContract("examples.json");
            JToken chat = examples["chatSuccess"], score = examples["scoreSuccess"];

            Assert.AreEqual(LlmErrorKind.InvalidScore, LlmApi.ParseScoreResponse((long)chat["status"], chat["body"].ToString(), false).Error);
            Assert.AreEqual(LlmErrorKind.Upstream, LlmApi.ParseChatResponse((long)score["status"], score["body"].ToString(), false).Error);
        }

        [Test, Description("契約のエラー応答は、どれも対応する種別の失敗として読める（未知の種別に落ちない）")]
        public void ErrorResponses_AreParsed()
        {
            var expected = new Dictionary<string, LlmErrorKind>
            {
                ["daily_quota"] = LlmErrorKind.DailyQuota,
                ["busy"] = LlmErrorKind.Busy,
                ["invalid_score"] = LlmErrorKind.InvalidScore,
                ["upstream"] = LlmErrorKind.Upstream,
                ["bad_request"] = LlmErrorKind.BadRequest,
                ["config"] = LlmErrorKind.Upstream,
            };

            foreach (JToken error in ReadContract("examples.json")["errors"])
            {
                string kind = (string)error["body"]["error"];
                Assert.IsTrue(expected.ContainsKey(kind), $"Unity 側で対応していない種別: {kind}");
                long status = (long)error["status"];
                string body = error["body"].ToString();
                Assert.AreEqual(expected[kind], LlmApi.ParseChatResponse(status, body, false).Error, "chat: " + kind);
                Assert.AreEqual(expected[kind], LlmApi.ParseScoreResponse(status, body, false).Error, "score: " + kind);
            }
        }
    }
}
