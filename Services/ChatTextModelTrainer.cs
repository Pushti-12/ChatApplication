using Microsoft.ML;
using Microsoft.ML.Data;
using ChatApplication.Models;

namespace ChatApplication.Services
{
    public class ChatTextModelTrainer
    {
        private readonly MLContext _mlContext = new MLContext(seed: 1);

        public void TrainModel()
        {
            string dataPath = Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "chat_text_classification_1000.csv"
            );

            IDataView data = _mlContext.Data.LoadFromTextFile<ChatTextData>(
                path: dataPath,
                hasHeader: true,
                separatorChar: ','
            );

            var split = _mlContext.Data.TrainTestSplit(
                data,
                testFraction: 0.2
            );

            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "LabelKey",
                    inputColumnName: nameof(ChatTextData.Label))
                .Append(_mlContext.Transforms.Text.FeaturizeText(
                    outputColumnName: "Features",
                    inputColumnName: nameof(ChatTextData.Question)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                    labelColumnName: "LabelKey",
                    featureColumnName: "Features"))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue(
                    outputColumnName: "PredictedLabel"));

            var model = pipeline.Fit(split.TrainSet);

            var predictions = model.Transform(split.TestSet);

            var metrics = _mlContext.MulticlassClassification.Evaluate(
                predictions,
                labelColumnName: "LabelKey",
                predictedLabelColumnName: "PredictedLabel"
            );

            Console.WriteLine($"Micro Accuracy: {metrics.MicroAccuracy:P2}");
            Console.WriteLine($"Macro Accuracy: {metrics.MacroAccuracy:P2}");

            string modelFolder = Path.Combine(
                AppContext.BaseDirectory,
                "MLModels"
            );

            Directory.CreateDirectory(modelFolder);

            string modelPath = Path.Combine(
                modelFolder,
                "chat-text-classifier.zip"
            );

            _mlContext.Model.Save(model, split.TrainSet.Schema, modelPath);

            Console.WriteLine($"Model saved at: {modelPath}");
        }
    }
}