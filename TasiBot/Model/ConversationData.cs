using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;

namespace TasiBot
{
    public class ConversationData
    {

        public enum AsiBotState
        {
            Ready,
            WaitingDisambiguation,
            WaitingApproval,
            EvaluatingQuickResponses
        }

        // rappresenta lo stato della conversazione
        public AsiBotState state { get; set; }
        // rappresenta l'ultimo messaggio inviato all'utente
        public Activity lastActivity { get; set; }
        // rappresenta il primo messaggio inviato dall'utente nell'intera conversazione
        public string originalMessage { get; set; }
        // rappresenta la possibile categoria di appartenenza della domanda posta dall'utente
        public string possibleCategory { get; set; }

    }
}
