using Microsoft.AspNetCore.Mvc;
using ChatApplication.Services;

namespace ChatApplication.Controllers
{
    public class MLTestController : Controller
    {
        private readonly ChatTextModelTrainer _trainer;

        public MLTestController()
        {
            _trainer = new ChatTextModelTrainer();
        }

        [HttpGet]
        public IActionResult Train()
        {
            _trainer.TrainModel();

            return Content(
                "Training completed. Check the application logs and MLModels folder."
            );
        }

        [HttpGet]
        public IActionResult Predict([FromQuery] string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return BadRequest("Please provide chat text using ?text=...");
            }

            var predictionService = new ChatApplication.Services.ChatTextPredictionService();

            string result = predictionService.Predict(text);

            return Content($"Message: {text}\nPrediction: {result}");
        }
    }
}