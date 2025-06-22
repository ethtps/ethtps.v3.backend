using System.Data;

using ETHTPS.V3.Data.Models;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ETHTPS.V3.Data
{
    public sealed class ETHTPSDatabase
    {
        private readonly string _connectionString;

        public ETHTPSDatabase(string connectionString)
        {
            _connectionString = connectionString;
        }

        public ETHTPSDatabase(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException("Connection string");
        }

        public int CreateProvider(
            string name,
            string type,
            string currencyName,
            string currencySymbol,
            int currencyDecimals,
            string infoUrl)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("[ProviderInfo].[CreateProvider]", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@Name", name);
            command.Parameters.AddWithValue("@Type", type);
            command.Parameters.AddWithValue("@CurrencyName", currencyName);
            command.Parameters.AddWithValue("@CurrencySymbol", currencySymbol);
            command.Parameters.AddWithValue("@CurrencyDecimals", currencyDecimals);
            command.Parameters.AddWithValue("@InfoURL", infoUrl);

            var outputId = new SqlParameter("@ID", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(outputId);

            connection.Open();
            command.ExecuteNonQuery();

            return (int)outputId.Value;
        }

        public void LinkRPCEndpoints(string providerName, List<string> urls)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            // Create the table-valued parameter
            var urlTable = new DataTable();
            urlTable.Columns.Add("URL", typeof(string));
            foreach (var url in urls)
                urlTable.Rows.Add(url);

            using var command = new SqlCommand("[ProviderInfo].[LinkRPCEndpoints]", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@ProviderName", providerName);
            var tvpParam = command.Parameters.AddWithValue("@URLs", urlTable);
            tvpParam.SqlDbType = SqlDbType.Structured;
            tvpParam.TypeName = "ProviderInfo.RPCEndpointList";

            command.ExecuteNonQuery();
        }

        public ProviderInfo? GetProvider(string name)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("[ProviderInfo].[GetProvider]", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@Name", name);

            connection.Open();
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new ProviderInfo
                {
                    ID = reader.GetInt32(reader.GetOrdinal("ID")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Type = reader.GetString(reader.GetOrdinal("Type")),
                    InfoURL = reader.IsDBNull(reader.GetOrdinal("InfoURL")) ? null : reader.GetString(reader.GetOrdinal("InfoURL")),
                    NativeCurrency = reader.GetString(reader.GetOrdinal("NativeCurrency")),
                    NativeCurrencySymbol = reader.GetString(reader.GetOrdinal("NativeCurrencySymbol"))
                };
            }

            return null;
        }

        public IEnumerable<ProviderInfo> GetAllProviders()
        {
            var results = new List<ProviderInfo>();

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("[ProviderInfo].[GetAllProviders]", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            connection.Open();
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var provider = new ProviderInfo
                {
                    ID = reader.GetInt32(reader.GetOrdinal("ID")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Type = reader.GetString(reader.GetOrdinal("Type")),
                    InfoURL = reader.IsDBNull(reader.GetOrdinal("InfoURL")) ? null : reader.GetString(reader.GetOrdinal("InfoURL")),
                    NativeCurrency = reader.GetString(reader.GetOrdinal("NativeCurrency")),
                    NativeCurrencySymbol = reader.GetString(reader.GetOrdinal("NativeCurrencySymbol"))
                };

                results.Add(provider);
            }

            return results;
        }

        public void LinkRpcEndpoints(string providerName, IEnumerable<string> urls)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("[ProviderInfo].[LinkRPCEndpoints]", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            // Add provider name parameter
            command.Parameters.AddWithValue("@ProviderName", providerName);

            // Create the table-valued parameter for URLs
            var urlTable = new DataTable();
            urlTable.Columns.Add("URL", typeof(string));
            foreach (var url in urls)
            {
                urlTable.Rows.Add(url);
            }

            var tvpParam = command.Parameters.AddWithValue("@URLs", urlTable);
            tvpParam.SqlDbType = SqlDbType.Structured;
            tvpParam.TypeName = "ProviderInfo.RPCEndpointList"; // Must match exactly your SQL-defined type

            // Execute the procedure
            connection.Open();
            command.ExecuteNonQuery();
        }
    }
}
