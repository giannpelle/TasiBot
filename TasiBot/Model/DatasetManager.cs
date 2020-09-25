using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace TasiBot
{
    public class DatasetManager
    {
        private SqlConnection DbConnection { get; }

        public DatasetManager(string serverName, string dbUser, string userPassword)
        {
            string connectionString = $"Data Source={serverName};Initial Catalog=MLDataset;User ID={dbUser};Password={userPassword}";
            DbConnection = new SqlConnection(connectionString);
            DbConnection.Open();
        }

        ~DatasetManager()
        {
            DbConnection.Close();
        }

        // aggiunge un record alla tabella Questions
        public void insertNewQuestion(string message, string category)
        {
            SqlDataAdapter adapter = new SqlDataAdapter();
            string sql = $"INSERT INTO Questions (Category, Question) values ('{category}', '{message}')";
            SqlCommand command = new SqlCommand(sql, DbConnection);
            adapter.InsertCommand = new SqlCommand(sql, DbConnection);
            adapter.InsertCommand.ExecuteNonQuery();
            command.Dispose();
        }

        // aggiunge un record alla tabella QuickResponsesQuestions
        public void insertNewWorkInProgressQuestion(string message, string category)
        {
            SqlDataAdapter adapter = new SqlDataAdapter();
            string sql = $"INSERT INTO QuickResponsesQuestions (Category, Question) values ('{category}', '{message}')";
            SqlCommand command = new SqlCommand(sql, DbConnection);
            adapter.InsertCommand = new SqlCommand(sql, DbConnection);
            adapter.InsertCommand.ExecuteNonQuery();
            command.Dispose();
        }

    }
}
