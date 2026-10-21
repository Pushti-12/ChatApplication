using Microsoft.ML;
using ChatApplication.Models;
using System.Linq;

namespace ChatApplication.Services
{
    public class ChatTextPredictionService
    {
        private readonly MLContext _mlContext;
        private readonly PredictionEngine<ChatTextData, ChatTextPrediction> _predictionEngine;

        public ChatTextPredictionService()
        {
            _mlContext = new MLContext();

            string modelPath = Path.Combine(
                AppContext.BaseDirectory,
                "MLModels",
                "chat-text-classifier.zip"
            );

            ITransformer model = _mlContext.Model.Load(
                modelPath,
                out var modelSchema
            );

            _predictionEngine = _mlContext.Model.CreatePredictionEngine
                <ChatTextData, ChatTextPrediction>(model);
        }


        public (string Label, float Confidence) PredictWithConfidence(
            string question)
        {
            var input = new ChatTextData
            {
                Question = question,
                Label = string.Empty
            };

            var prediction = _predictionEngine.Predict(input);
            var scores = prediction.Score;

            if (scores == null || scores.Length == 0)
            {
                return (prediction.PredictedLabel, 0f);
            }

            // Normalize non-negative class scores.
            var maxScore = scores.Max();
            var totalScore = scores.Sum();

            float confidence = totalScore > 0 && scores.All(s => s >= 0)
                ? maxScore / totalScore
                : 0f;

            return (
                prediction.PredictedLabel,
                Math.Clamp(confidence, 0f, 1f)
            );
        }

        public string Predict(string question)
        {
            var result = PredictWithConfidence(question);
            return result.Label;
        }

    }
}