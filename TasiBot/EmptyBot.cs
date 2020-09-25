// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Connector;
using Microsoft.ML;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Security.Policy;
using AdaptiveCards;
using Newtonsoft.Json.Linq;

namespace TasiBot
{

    public class EmptyBot : ActivityHandler
    {

        // rappresenta una stuttura dati con cui convertire facilmente i dati in messaggi da inviare nella conversazione
        public struct ChatBotMessage
        {
            // rappresenta un array di stringhe da inviare in messaggi distinti
            public string[] TextMessages;
            // rappresenta un array di bottoni di risposta da porre alla fine di tutti i messaggi
            public string[] Options;
            public ChatBotMessage(string[] textMessages, string[] options)
            {
                TextMessages = textMessages;
                Options = options;
            }

            public ChatBotMessage(string[] textMessages)
            {
                TextMessages = textMessages;
                Options = new string[] { };
            }

            // restituisce l'array di activities generato con i dati presenti nella struttura dati
            public List<Activity> GetActivities()
            {
                var activities = new List<Activity> ();

                for (int i = 0; i < TextMessages.Length; i++)
                {
                    if ((i == TextMessages.Length - 1) && (Options.Length != 0))
                    {
                        // se un messaggio contiene un url definito dal markup -url-indirizzo-url- viene suddiviso in più messaggi distinti per inviare l'url in un messaggio singolo di più comoda interazione
                        if (TextMessages[i].Contains("-url-"))
                        {
                            var urlActivities = GetActivitiesFromMessageWithUrl(TextMessages[i]);
                            
                            for (int j = 0; j < urlActivities.Count; j++)
                            {
                                if (j == TextMessages.Length - 1)
                                {
                                    activities.Add(urlActivities[i]);
                                } else
                                {
                                    var actions = Options.Select(option => new CardAction() { Title = option, Type = ActionTypes.ImBack, Value = option }).ToArray();
                                    urlActivities[i].SuggestedActions = new SuggestedActions() { Actions = actions };
                                    activities.Add(urlActivities[i]);
                                }
                            }
                            
                        } else
                        {
                            var activity = MessageFactory.Text(TextMessages[i]);
                            var actions = Options.Select(option => new CardAction() { Title = option, Type = ActionTypes.ImBack, Value = option }).ToArray();
                            activity.SuggestedActions = new SuggestedActions() { Actions = actions };
                            activities.Add(activity);
                        }
                        
                    } else
                    {
                        if (TextMessages[i].Contains("-url-"))
                        {
                            activities = activities.Concat(GetActivitiesFromMessageWithUrl(TextMessages[i])).ToList();
                        } else
                        {
                            var activity = MessageFactory.Text(TextMessages[i]);
                            activities.Add(activity);
                        }
                    }
                }

                return activities;
            }

            // se un messaggio contiene un url definito dal markup -url-indirizzo-url- viene suddiviso in più messaggi distinti per inviare l'url in un messaggio singolo di più comoda interazione
            private List<Activity> GetActivitiesFromMessageWithUrl(string message)
            {
                string cleanMessage = message.Trim();
                string[] segments = cleanMessage.Split("-url-");

                List<Activity> activities = new List<Activity> ();

                for (int i = 0; i < segments.Length; i++)
                {
                    if ((i == 0 && String.IsNullOrEmpty(segments.First())) || ((i == segments.Length - 1) && String.IsNullOrEmpty(segments.Last()))) continue;

                    if (i % 2 == 0)
                    {
                        activities.Add(MessageFactory.Text(segments[i]));
                    } else
                    {
                        var activity = MessageFactory.Text(segments[i]);
                        activities.Add(activity);
                    }
                }

                return activities;
            }
        }

        private BotState _conversationState;
        private MLManager<AsiQuestion, QuestionPrediction> _mlManager;
        private DatasetManager _datasetManager;

        public EmptyBot(ConversationState conversationState)
        {
            _conversationState = conversationState;
            string appPath = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            string modelPath = Path.Combine(appPath, "..", "..", "..", "ML", "MLModel", "modello.zip");
            _mlManager = new MLManager<AsiQuestion, QuestionPrediction>(modelPath);
            _datasetManager = new DatasetManager("laptop", "prova", "prova");
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // salva lo stato della conversazione a ogni turno della conversazione
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        // metodo che viene invocato per ogni messaggio che viene inviato dall'utente
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            // creo un accessor per accedere ai dati salvati della conversazione
            var conversationStateAccessors = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());

            // ottengo la stringa del messaggio inviato dall'utente
            string messageText = turnContext.Activity.Text;

            switch (conversationData.state)
            {
                case ConversationData.AsiBotState.Ready:
                    AsiQuestion singleQuestion = new AsiQuestion() { Question = messageText };
                    var categoriesReponse = _mlManager.MakePrediction(singleQuestion);

                    if (categoriesReponse.Count == 0)
                    {
                        var noCategoryMessage = new ChatBotMessage(new string[] { "Non è stato possibile identificare la categoria del problema, si prega di contattare l'assistenza clienti alla mail info@info.it" });
                        typeAnswers(noCategoryMessage.GetActivities(), turnContext, cancellationToken);
                        conversationData.state = ConversationData.AsiBotState.Ready;
                        conversationData.lastActivity = MessageFactory.Text("");
                        conversationData.originalMessage = "";
                        conversationData.possibleCategory = "";
                    } else if (categoriesReponse.Count == 1)
                    {
                        string firstMessage = $"Categoria trovata: {categoriesReponse.First().getCategoryName()}";
                        string secondMessage = $"Puoi provare a risolvere il problema al seguente link -url-{categoriesReponse.First().getDocumentationUrl()}-url-";
                        string thirdMessage = "Questa risposta ti è stata utile?";
                        var oneCategoryMessage = new ChatBotMessage(new string[] { firstMessage, secondMessage, thirdMessage }, new string[] { "Si", "No" });
                        var oneCategoryActivities = oneCategoryMessage.GetActivities();
                        typeAnswers(oneCategoryActivities, turnContext, cancellationToken);
                        conversationData.state = ConversationData.AsiBotState.WaitingApproval;
                        conversationData.lastActivity = oneCategoryActivities.Last();
                        conversationData.originalMessage = messageText;
                        conversationData.possibleCategory = categoriesReponse.First().getCategoryName();
                    } else
                    {
                        var ambiguosMessage = new ChatBotMessage(new string[] { "Di quali di queste categorie stai parlando?" }, categoriesReponse.Select(category => category.getCategoryName()).ToArray());
                        var ambiguosActivities = ambiguosMessage.GetActivities();
                        typeAnswers(ambiguosActivities, turnContext, cancellationToken);
                        conversationData.state = ConversationData.AsiBotState.WaitingDisambiguation;
                        conversationData.lastActivity = ambiguosActivities.Last();
                        conversationData.originalMessage = messageText;
                        conversationData.possibleCategory = "";
                    }

                    break;

                case ConversationData.AsiBotState.WaitingDisambiguation:
                    var inputCategory = new AsiCategoryHandler(messageText);

                    // se l'utente non risponde correttamente alla domanda, gli viene riproposta rimanendo nello stesso stato
                    if (inputCategory.category == AsiCategoryHandler.AsiCategory.Undefined)
                    {
                        await turnContext.SendActivityAsync(conversationData.lastActivity);
                        return;
                    }

                    var disambiguationMessage = new ChatBotMessage() { TextMessages = new string[] { $"Il manuale di riferimento è disponibile al link -url-{inputCategory.getDocumentationUrl()}-url-", "Ti è stato utile?" }, Options = new string[] { "Si", "No" } };
                    var activities = disambiguationMessage.GetActivities();
                    typeAnswers(activities, turnContext, cancellationToken);
                    conversationData.lastActivity = activities.Last();
                    conversationData.state = ConversationData.AsiBotState.WaitingApproval;
                    conversationData.possibleCategory = inputCategory.getCategoryName();

                    break;

                case ConversationData.AsiBotState.WaitingApproval:

                    // se l'utente non risponde Si o No gli viene riproposta la domanda
                    if (!messageText.Equals("Si") && !messageText.Equals("No")) 
                    {
                        await turnContext.SendActivityAsync(conversationData.lastActivity, cancellationToken);
                        return;
                    }

                    if (messageText.Equals("Si"))
                    {
                        _datasetManager.insertNewQuestion(conversationData.originalMessage, conversationData.possibleCategory);
                        await turnContext.SendActivityAsync(MessageFactory.Text("Grazie per aver utilizzato il nostro servizio, arrivederci"));
                        
                        conversationData.lastActivity = MessageFactory.Text("");
                        conversationData.state = ConversationData.AsiBotState.Ready;
                        conversationData.originalMessage = "";
                        conversationData.possibleCategory = "";
                    } else
                    {
                        var quickResponsesMessage = generateWorkInProgressActivity();

                        await turnContext.SendActivityAsync(quickResponsesMessage, cancellationToken);

                        conversationData.state = ConversationData.AsiBotState.EvaluatingQuickResponses;
                        conversationData.possibleCategory = "";
                    }

                    break;

                case ConversationData.AsiBotState.EvaluatingQuickResponses:

                    var inCategory = new QuickResponseCategoryHandler(messageText);
                    if (inCategory.category == QuickResponseCategoryHandler.QuickResponseCategory.Undefined && !(messageText.Equals("Nessuna delle precedenti")))
                    {
                        await turnContext.SendActivityAsync(generateWorkInProgressActivity());
                        return;
                    }

                    if (inCategory.category == QuickResponseCategoryHandler.QuickResponseCategory.Undefined)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Ci dispiace non essere riusciti a risolvere il tuo problema, buona giornata"));
                    } else
                    {
                        _datasetManager.insertNewWorkInProgressQuestion(conversationData.originalMessage, inCategory.getCategoryName());
                        await turnContext.SendActivityAsync(MessageFactory.Text("Grazie per il tuo feedback e per aver utilizzato il nostro servizio, arrivederci"));
                    }

                    conversationData.lastActivity = MessageFactory.Text("");
                    conversationData.state = ConversationData.AsiBotState.Ready;
                    conversationData.originalMessage = "";
                    conversationData.possibleCategory = "";

                    break;
            }

        }

        // simula la scrittura in real time di un array di activity
        public async void typeAnswers(List<Activity> activities, ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (Activity activity in activities)
            {
                await turnContext.SendActivitiesAsync(
                    new Activity[] {
                        new Activity { Type = ActivityTypes.Typing },
                        new Activity { Type = "delay", Value= 1200 },
                        activity,
                    },
                    cancellationToken);
            }
        }

        // genera l'activity contenente la card per l'acquisizione dati del nuovo servizio
        public Activity generateWorkInProgressActivity()
        {
            Activity quickResponsesMessage = MessageFactory.Text("New feature: Work in progress");
            quickResponsesMessage.Attachments = new List<Attachment>();

            string cardTitle = "Aiutaci a migliorare il servizio";
            string cardSubtitle = "Sfortunatamente non siamo riusciti a risolvere il problema da lei richiesto ma stiamo lavorando allo sviluppo di un nuovo servizio di risoluzione dei problemi più comuni. Troverà di seguito una lista di soluzioni comuni per problemi di dominio generale, se eventualmente una delle soluzioni risolvesse il suo problema sarebe gradito un suo feedback per aiutare lo sviluppo del nuovo servizio, grazie";
            var quickResponses = QuickResponseCategoryHandler.GetQuickResponsesList();
            string cardJSON = generateAdaptiveCardWithOptions(cardTitle, cardSubtitle, quickResponses);

            var results = AdaptiveCard.FromJson(cardJSON);
            var card = results.Card;
            quickResponsesMessage.Attachments.Add(new Attachment()
            {
                Content = card,
                ContentType = AdaptiveCard.ContentType,
                Name = "Card"
            });

            return quickResponsesMessage;
        }

        // genera l'adaptive card da allegare all'activity del messaggio di work in progress
        public string generateAdaptiveCardWithOptions(string title, string subtitle, List<QuickResponseCategoryHandler> quickResponses)
        {
            AdaptiveCard card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0));

            card.Body.Add(new AdaptiveTextBlock()
            {
                Text = title,
                Size = AdaptiveTextSize.Medium,
                Weight = AdaptiveTextWeight.Bolder
            });

            DateTime localDate = DateTime.Now;
            var culture = new CultureInfo("it-IT");
            string time = localDate.ToString(culture);

            card.Body.Add(new AdaptiveColumnSet()
            {
                Columns = new List<AdaptiveColumn>() 
                {
                    new AdaptiveColumn()
                    {
                        Items = new List<AdaptiveElement>()
                        {
                            new AdaptiveImage()
                            {
                                Style = AdaptiveImageStyle.Person,
                                Url = new Uri("http://localhost:3978/logo.png"),
                                Size = AdaptiveImageSize.Small
                            }
                        },
                        Width = "auto"
                    },
                    new AdaptiveColumn()
                    {
                        Items = new List<AdaptiveElement>()
                        {
                            new AdaptiveTextBlock()
                            {
                                Text = "Asi Bot",
                                Weight = AdaptiveTextWeight.Bolder,
                                Wrap = true
                            },
                            new AdaptiveTextBlock()
                            {
                                Text = $"Created at {time}",
                                Spacing = AdaptiveSpacing.None,
                                IsSubtle = true,
                                Wrap = true
                            }
                        },
                        Width = "stretch"
                    }
                }
            });

            card.Body.Add(new AdaptiveTextBlock()
            {
                Text = subtitle,
                Wrap = true
            });

            var facts = quickResponses.Select(quickResponse => new AdaptiveFact() { Title = quickResponse.getAlphabeticIndex(), Value = quickResponse.getCategoryDescription() }).ToList();

            card.Body.Add(new AdaptiveFactSet()
            {
                Facts = facts
            });

            var myActions = new List<AdaptiveAction>(quickResponses.Select(quickResponse => new AdaptiveSubmitAction() { Type = "Action.Submit", Title = $"{quickResponse.getAlphabeticIndex()}) {quickResponse.getCategoryName()}", Data = $"{quickResponse.getCategoryName()}" }).ToList());

            myActions.Add(new AdaptiveSubmitAction() { Type = "Action.Submit", Title = "Nessuna delle precedenti", Data = "Nessuna delle precedenti" });
            card.Actions = myActions;

            // serialize the card to JSON
            string json = card.ToJson();
            return json;
        }

        // metodo che viene chiamato per ogni nuova istanza del bot che viene creata per ogni nuova conversazione
        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Benvenuto nel canale di assistenza del gestionale erp, Come posso esserle d'aiuto?"), cancellationToken);
                }
            }
        }
    }
}
