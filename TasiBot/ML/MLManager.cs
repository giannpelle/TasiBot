using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.Trainers.LightGbm;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Bot.Builder;

namespace TasiBot
{
    public interface PredictionInterface
    {
        public Single[] Score { get; set; }
        public string Category { get; set; }
    }

    public class MLManager<T, U> where U : class, PredictionInterface, new() where T : class, new()
    {

        // struttura dati che rappresenta il risultato della predizione del modello ML
        private struct ReliabilityResponse
        {
            public bool IsReliable
            {
                get
                {
                    return AmbiguousCategories.Length == 1;
                }
            }

            // contiene l'array di categorie tra cui è indeciso il modello in formato testuale descending per score
            public string[] AmbiguousCategories;
            public ReliabilityResponse(string[] ambiguousCategories)
            {
                AmbiguousCategories = ambiguousCategories;
            }
        }

        private string modelPath;
        private MLContext mlContext;

        public MLManager(string modelPath)
        {
            this.modelPath = modelPath;
            this.mlContext = new MLContext();
        }

        public List<AsiCategoryHandler> MakePrediction(T modelInputData)
        {
            // carico il modello ML a partire dal path dello zip
            ITransformer loadedModel = mlContext.Model.Load(modelPath, out var modelInputSchema);
            // creo l'engine per l'elaborazione della predizione
            var predictionEngine = mlContext.Model.CreatePredictionEngine<T, U>(loadedModel);
            var prediction = predictionEngine.Predict(modelInputData);

            ReliabilityResponse predictionReliabilityResponse = IsPredictionReliable(convertScoresArrayIntoScoresDictionary(predictionEngine.OutputSchema, "Score", prediction.Score));

            if (predictionReliabilityResponse.IsReliable)
            {
                return new List<AsiCategoryHandler>() { new AsiCategoryHandler(prediction.Category) };

            // se è indeciso fino a un massimo di 3 categorie probabilmente è una delle 3, se invece sono 4 o più è totalmente indeciso 
            } else if (predictionReliabilityResponse.AmbiguousCategories.Length < 4)
            {
                return predictionReliabilityResponse.AmbiguousCategories.Select(category => new AsiCategoryHandler(category)).ToList();

            } else
            {
                return new List<AsiCategoryHandler>() { };
            }

        }

        // converte l'array di scores della predizione in un dizionario con chiave categoria e valore score relativo
        private Dictionary<string, float> convertScoresArrayIntoScoresDictionary(DataViewSchema schema, string name, float[] scores)
        {
            Dictionary<string, float> result = new Dictionary<string, float>();
            var column = schema.GetColumnOrNull(name);

            var slotNames = new VBuffer<ReadOnlyMemory<char>>();
            column.Value.GetSlotNames(ref slotNames);
            var names = new string[slotNames.Length];
            var num = 0;
            foreach (var denseValue in slotNames.DenseValues())
            {
                result.Add(denseValue.ToString(), scores[num++]);
            }

            return result.OrderByDescending(c => c.Value).ToDictionary(i => i.Key, i => i.Value);
        }

        // Calcola l'affidabilità della predizione a partire dal dizionario di scores
        private ReliabilityResponse IsPredictionReliable(Dictionary<string, float> scoresDict)
        {
            if (scoresDict.Count == 0)
                return new ReliabilityResponse(new string[] { });

            if (scoresDict.Count == 1)
                return new ReliabilityResponse(scoresDict.Keys.ToArray());

            // converto gli scores in interi moltiplicati per 100
            var intScores = new List<KeyValuePair<string, int>> ();
            foreach (var score in scoresDict)
            {
                intScores.Add(new KeyValuePair<string, int> (score.Key, Convert.ToInt32(Math.Round(score.Value * 100))));
            }

            // ordino gli scores in ordine decrescente
            var descendingIntScores = intScores.OrderBy(score => score.Value).Reverse().ToList();

            int[] varianceScores = new int[descendingIntScores.Count];

            // calcolo l'array delle variazioni tra lo scoring delle categorie a coppie
            for (int i = 0; i < varianceScores.Length - 1; i++)
            {
                varianceScores[i] = (descendingIntScores[i].Value - descendingIntScores[i + 1].Value);
            }
            varianceScores.Append(descendingIntScores.Last().Value); 

            int[] varianceSquareSum = new int[varianceScores.Length];

            // calcolo l'array memoizzato delle somme dei quadrati delle variazioni dalla fine dell'array fino al secondo elemento
            for (int j = varianceScores.Length - 1; j >= 0; j--)
            {
                if (j == varianceScores.Length - 1)
                {
                    varianceSquareSum[j] = Convert.ToInt32(Math.Pow(varianceScores[j], 2));
                    continue;
                }

                varianceSquareSum[j] = varianceSquareSum[j + 1] + Convert.ToInt32(Math.Pow(varianceScores[j], 2));
            }

            int ambiguousIndexesCounter = 1;

            // se il quadrato della varianza di un elemento è maggiore della somma dei quadrati delle varianze di tutti gli elementi con scores inferiori lo reputo dominante e quindi affidabile
            for (int z = 0; z < descendingIntScores.Count - 1; z++)
            {
                if (Math.Pow(varianceScores[z], 2) > varianceSquareSum[z+1])
                {
                    // se più elementi risultano dominanti li restituisco tutti quanti ma da disambiguare
                    return (ambiguousIndexesCounter < 2) ? new ReliabilityResponse(new string[] { descendingIntScores.First().Key }) : new ReliabilityResponse(descendingIntScores.Select(score => score.Key).Take(ambiguousIndexesCounter).ToArray());
                }
                ambiguousIndexesCounter++;
            }

            return new ReliabilityResponse(descendingIntScores.Select(score => score.Key).Take(ambiguousIndexesCounter).ToArray());

        }

    }
}

