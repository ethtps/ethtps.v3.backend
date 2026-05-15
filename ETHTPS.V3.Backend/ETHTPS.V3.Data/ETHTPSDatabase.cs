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

        public IEnumerable<DataUpdater> GetAllUpdaters()
        {
            var results = new List<DataUpdater>();

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("[DataUpdaters].[GetAllUpdaters]", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            connection.Open();
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var updater = new DataUpdater
                {
                    ID = reader.GetInt32(reader.GetOrdinal("ID")),
                    Type = reader.GetString(reader.GetOrdinal("Type")),
                    Provider = reader.GetString(reader.GetOrdinal("Provider")),
                    Enabled = reader.GetBoolean(reader.GetOrdinal("Enabled"))
                };

                results.Add(updater);
            }

            return results;
        }

        public IEnumerable<EndpointMetadata> GetAllEndpointMetadata()
        {
            var results = new List<EndpointMetadata>();

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("[DataUpdaters].[GetAllMetadata]", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            connection.Open();
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                object lastHitValue = reader.GetValue(reader.GetOrdinal("LastHit"));
                DateTime? lastHit = default;
                if (lastHitValue != DBNull.Value)
                {
                    lastHit = (DateTime)lastHitValue;
                }
                var updater = new EndpointMetadata
                {
                    ID = reader.GetInt32(reader.GetOrdinal("ID")),
                    Provider = reader.GetString(reader.GetOrdinal("Provider")),
                    Enabled = reader.GetBoolean(reader.GetOrdinal("Enabled")),
                    Healthy = reader.GetBoolean(reader.GetOrdinal("Healthy")),
                    URL = reader.GetString(reader.GetOrdinal("URL")),
                    LastHit = lastHit,
                    FailureCount = reader.GetInt32(reader.GetOrdinal("FailureCount")),
                    HitCount = reader.GetInt32(reader.GetOrdinal("HitCount")),
                    AverageAccessTimeMs = reader.GetInt32(reader.GetOrdinal("AverageAccessTimeMs")),
                };

                results.Add(updater);
            }

            return results;
        }

        public async Task BulkUpdateRPCMetadataAsync(IEnumerable<EndpointMetadata> metadataList)
        {
            var dataTable = new DataTable();
            dataTable.Columns.Add("ID", typeof(int));
            dataTable.Columns.Add("Enabled", typeof(bool));
            dataTable.Columns.Add("Healthy", typeof(bool));
            dataTable.Columns.Add("LastHit", typeof(DateTime));
            dataTable.Columns.Add("HitCount", typeof(int));
            dataTable.Columns.Add("FailureCount", typeof(int));
            dataTable.Columns.Add("AverageAccessTimeMs", typeof(int));

            foreach (var item in metadataList)
            {
                dataTable.Rows.Add(
                    item.ID,
                    item.Enabled,
                    item.Healthy,
                    item.LastHit,
                    item.HitCount,
                    item.FailureCount,
                    item.AverageAccessTimeMs
                );
            }

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("[DataUpdaters].[UpdateRPCMetadataBulk]", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            var param = cmd.Parameters.AddWithValue("@Updates", dataTable);
            param.SqlDbType = SqlDbType.Structured;
            param.TypeName = "[DataUpdaters].[RPCMetadataUpdateType]";

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

    }
}
