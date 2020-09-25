using System;
using System.Collections.Generic;
using Microsoft.ML.Data;
using TasiBot;

public class AsiQuestion
{
    [LoadColumn(0), ColumnName("Category")]
    public string Category { get; set; }
    [LoadColumn(1), ColumnName("Question")]
    public string Question { get; set; }
}

public class QuestionPrediction: PredictionInterface
{
    [ColumnName("PredictedLabel")]
    public string Category { get; set; }

    [ColumnName("Score")]
    public Single[] Score { get; set; }
}

public class AsiCategoryHandler
{
    public enum AsiCategory : int
    {
        ErpIndustria = 0,
        ErpCommercio,
        ErpABC,
        ErpEFattura,
        PiattaformaGestioneFatture,
        Undefined = 99
    }

    public AsiCategory category { get; }

    public AsiCategoryHandler(string categoryName)
    {
        switch (categoryName)
        {
            case "ERP industria":
                category = AsiCategory.ErpIndustria;
                break;
            case "ERP commercio":
                category = AsiCategory.ErpCommercio;
                break;
            case "ERP A.B.C.":
                category = AsiCategory.ErpABC;
                break;
            case "ERP E-Fattura":
                category = AsiCategory.ErpEFattura;
                break;
            case "Documentale piattaforma gestione flussi fatture elettroniche":
                category = AsiCategory.PiattaformaGestioneFatture;
                break;
            default:
                category = AsiCategory.Undefined;
                break;
        }
    }

    public string getCategoryName()
    {
        switch (category)
        {
            case AsiCategory.ErpIndustria:
                return "ERP industria";
            case AsiCategory.ErpCommercio:
                return "ERP commercio";
            case AsiCategory.ErpABC:
                return "ERP A.B.C.";
            case AsiCategory.ErpEFattura:
                return "ERP E-Fattura";
            case AsiCategory.PiattaformaGestioneFatture:
                return "Documentale piattaforma gestione flussi fatture elettroniche";
            default:
                return "undefined";
        }
    }

    public string getDocumentationUrl()
    {
        switch (category)
        {
            case AsiCategory.ErpIndustria:
                return "https://erp-industria.it/";
            case AsiCategory.ErpCommercio:
                return "https://erp-commercio.it/";
            case AsiCategory.ErpABC:
                return "https://erp-abc.it/";
            case AsiCategory.ErpEFattura:
                return "https://erp-efattura.it/";
            case AsiCategory.PiattaformaGestioneFatture:
                return "https://erp-fatture.it/";
            default:
                return "https://www.google.it/";
        }
    }
}

public class QuickResponseCategoryHandler
{
    public static List<QuickResponseCategoryHandler> GetQuickResponsesList()
    {
        return new List<QuickResponseCategoryHandler>() {
            new QuickResponseCategoryHandler("Problemi di connessione"),
            new QuickResponseCategoryHandler("Servizio scaduto"),
            new QuickResponseCategoryHandler("Stampante non disponibile"),
            new QuickResponseCategoryHandler("Riavvio manuale del sistema")
        };
    }

    public enum QuickResponseCategory : int
    {
        ConnectionProblem = 0,
        ExpiredService,
        PrinterNotAvailable,
        RestartNeeded,
        Undefined = 99
    }

    public QuickResponseCategory category { get; }

    public QuickResponseCategoryHandler(string categoryName)
    {
        switch (categoryName)
        {
            case "Problemi di connessione":
                category = QuickResponseCategory.ConnectionProblem;
                break;
            case "Servizio scaduto":
                category = QuickResponseCategory.ExpiredService;
                break;
            case "Stampante non disponibile":
                category = QuickResponseCategory.PrinterNotAvailable;
                break;
            case "Riavvio manuale del sistema":
                category = QuickResponseCategory.RestartNeeded;
                break;
            default:
                category = QuickResponseCategory.Undefined;
                break;
        }
    }

    public string getCategoryName()
    {
        switch (category)
        {
            case QuickResponseCategory.ConnectionProblem:
                return "Problemi di connessione";
            case QuickResponseCategory.ExpiredService:
                return "Servizio scaduto";
            case QuickResponseCategory.PrinterNotAvailable:
                return "Stampante non disponibile";
            case QuickResponseCategory.RestartNeeded:
                return "Riavvio manuale del sistema";
            default:
                return "undefined";
        }
    }

    public string getCategoryDescription()
    {
        switch (category)
        {
            case QuickResponseCategory.ConnectionProblem:
                return "Controlla di avere una connessione a internet stabile e funzionante";
            case QuickResponseCategory.ExpiredService:
                return "Controlla che l'abbonamento al servizio non sia scaduto";
            case QuickResponseCategory.PrinterNotAvailable:
                return "Controlla che la stampante sia collegata correttamente al computer facendo una stampa di prova";
            case QuickResponseCategory.RestartNeeded:
                return "Prova a spegnere e riaccendere il computer e controlla se il problema persiste";
            default:
                return "undefined";
        }
    }

    public string getAlphabeticIndex()
    {
        int charIndex = (int) this.category + 65;
        return ((char)charIndex).ToString();
    }
}