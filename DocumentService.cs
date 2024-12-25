using Microsoft.AspNetCore.Http.HttpResults;
using Oracle.ManagedDataAccess.Client;
using Serilog;
using System.Data;

namespace NalogZaUtovar;

public class DocumentService
{
    private readonly string _connectionString;

    public DocumentService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<byte[]?> GetDocumentByDetailsAsync(string skladiste, string kupac, string kamion, int nalog)
    {
        string query = @"
            SELECT DOKUMENT
            FROM NU_DOKUMENTI
            WHERE SKLADISTE = :Skladiste
              AND KUPAC = :Kupac
              AND REGISTRACIJA_VOZILA = :Kamion
              AND BROJ_NALOGA = :Nalog
              AND STATUS = 'A'";

        return await ExecuteQueryAsync(query, new OracleParameter("Skladiste", skladiste),
                                             new OracleParameter("Kupac", kupac),
                                             new OracleParameter("Kamion", kamion),
                                             new OracleParameter("Nalog", nalog));
    }

    public async Task<byte[]?> GetDocumentByOrderNumberAsync(int brojNaloga)
    {
        const string query = @"
            SELECT DOKUMENT
            FROM NU_DOKUMENTI
            WHERE BROJ_NALOGA = :BrojNaloga AND STATUS = 'A'";

        return await ExecuteQueryAsync(query, new OracleParameter("BrojNaloga", brojNaloga));
    }

    public async Task<List<string>> GetAllSkladista()
    {
        var skladista = new List<string>();

        const string query = "SELECT DISTINCT SKLADISTE FROM NU_DOKUMENTI";

        using (var connection = new OracleConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = new OracleCommand(query, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        skladista.Add(reader.GetString(0));
                    }
                }
            }
        }

        return skladista;
    }

    public async Task<List<string>> GetKupciPoSkladistu(string skladiste)
    {
        var customers = new List<string>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        string query = "SELECT DISTINCT KUPAC FROM NU_DOKUMENTI WHERE SKLADISTE = :Skladiste AND STATUS = 'A'";
        using var command = new OracleCommand(query, connection);
        command.Parameters.Add(new OracleParameter("Skladiste", skladiste));

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            customers.Add(reader.GetString(0));
        }

        return customers;
    }

    public async Task<List<string>> GetKamioniPoSkladistuIKupcu(string skladiste, string kupac)
    {
        var kamioni = new List<string>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        string query = @"
        SELECT DISTINCT REGISTRACIJA_VOZILA 
        FROM NU_DOKUMENTI 
        WHERE SKLADISTE = :Skladiste AND KUPAC = :Kupac AND STATUS = 'A'";

        using var command = new OracleCommand(query, connection);
        command.Parameters.Add(new OracleParameter("Skladiste", skladiste));
        command.Parameters.Add(new OracleParameter("Kupac", kupac));

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            kamioni.Add(reader.GetString(0));
        }

        return kamioni;
    }

    public async Task<List<int>> GetNaloziPoSkladistuKupcuKamionu(string skladiste, string kupac, string kamion)
    {
        var nalozi = new List<int>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        string query = @"
        SELECT DISTINCT BROJ_NALOGA 
        FROM NU_DOKUMENTI 
        WHERE SKLADISTE = :Skladiste AND KUPAC = :Kupac AND REGISTRACIJA_VOZILA = :Kamion AND STATUS = 'A'";

        using var command = new OracleCommand(query, connection);
        command.Parameters.Add(new OracleParameter("Skladiste", skladiste));
        command.Parameters.Add(new OracleParameter("Kupac", kupac));
        command.Parameters.Add(new OracleParameter("Kamion", kamion));


        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            nalozi.Add(reader.GetInt32(0));
        }

        return nalozi;
    }

    public async Task PostNalog(NalogPostDto request)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        string query = @"
        INSERT INTO NU_DOKUMENTI (SKLADISTE, KUPAC, REGISTRACIJA_VOZILA, GODINA_NALOGA, BROJ_NALOGA, DOKUMENT, QR_SINGL, QR_FULL)
        VALUES (:Skladiste, :Kupac, :RegistracijaVozila, :GodinaNaloga, :BrojNaloga, :Dokument, :QrSingl, :QrFull)";

        using var command = new OracleCommand(query, connection);
        command.Parameters.Add(new OracleParameter("Skladiste", request.Skladiste));
        command.Parameters.Add(new OracleParameter("Kupac", request.Kupac));
        command.Parameters.Add(new OracleParameter("RegistracijaVozila", request.RegistracijaVozila));
        command.Parameters.Add(new OracleParameter("GodinaNaloga", request.GodinaNaloga ?? (object)DBNull.Value));
        command.Parameters.Add(new OracleParameter("BrojNaloga", request.BrojNaloga));
        command.Parameters.Add(new OracleParameter("Dokument", request.Dokument));
        command.Parameters.Add(new OracleParameter("QrSingl", request.QrSingl ?? (object)DBNull.Value /*qrSinglBytes ?? (object)DBNull.Value)*/));
        command.Parameters.Add(new OracleParameter("QrFull", request.QrFull ?? (object)DBNull.Value /*qrFullBytes ?? (object)DBNull.Value)*/));

        await command.ExecuteNonQueryAsync();
    }

    public async Task PostMultipleNalozi(List<NalogPostDto>? requestListInsert, List<NalogPostDto>? requestListUpdate)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            string queryInsert = @"
            INSERT INTO NU_DOKUMENTI (SKLADISTE, KUPAC, REGISTRACIJA_VOZILA, GODINA_NALOGA, BROJ_NALOGA, DOKUMENT, QR_SINGL, QR_FULL)
            VALUES (:Skladiste, :Kupac, :RegistracijaVozila, :GodinaNaloga, :BrojNaloga, :Dokument, :QrSingl, :QrFull)";

            string queryUpdate = @"
                UPDATE NU_DOKUMENTI 
                SET SKLADISTE = :Skladiste, KUPAC = :Kupac, REGISTRACIJA_VOZILA = :RegistracijaVozila, GODINA_NALOGA = :GodinaNaloga, BROJ_NALOGA = : BrojNaloga, DOKUMENT = :Dokument, QR_SINGL = :QrSingl, QR_FULL = :QrFull 
                WHERE BROJ_NALOGA = :BrojNaloga AND STATUS = 'A'";

            if (requestListInsert != null && requestListInsert.Count != 0)
            {
                await InsertUpdateNalozi(requestListInsert, connection, transaction, queryInsert);
            }

            if (requestListUpdate != null && requestListUpdate.Count != 0)
            {

                await UpdateOrInsertNalozi(requestListUpdate, connection, transaction, queryUpdate, queryInsert);
            }

            await transaction.CommitAsync();
            Log.Information("USPESNA TRANSAKCIJA!!!");
        }
        catch(OracleException ex)
        {
            Console.WriteLine($"Error: {ex.Message}, Code: {ex.Number}");
            Log.Error("Greska kod transakcije: ", ex.Message);
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task InsertUpdateNalozi(List<NalogPostDto> requestListInsertOrUpdate, OracleConnection connection, OracleTransaction transaction, string query)
    {
        try
        {
            foreach (var request in requestListInsertOrUpdate)
            {
                using var command = new OracleCommand(query, connection);
                command.Transaction = transaction;

                AddParametersToCommand(command, request);

                await command.ExecuteNonQueryAsync();
                Log.Information("USPESAN INSERT");
            }
        }
        catch (OracleException ex)
        {
            Console.WriteLine($"Error: {ex.Message}, Code: {ex.Number}");
            Log.Error($"Error: {ex.Message}, Code: {ex.Number}");
            throw;
        }
        
    }

    private static void AddParametersToCommand(OracleCommand command, NalogPostDto request)
    {
        command.Parameters.Add(new OracleParameter("Skladiste", request.Skladiste));
        command.Parameters.Add(new OracleParameter("Kupac", request.Kupac));
        command.Parameters.Add(new OracleParameter("RegistracijaVozila", request.RegistracijaVozila));
        command.Parameters.Add(new OracleParameter("GodinaNaloga", request.GodinaNaloga));
        command.Parameters.Add(new OracleParameter("BrojNaloga", request.BrojNaloga));
        command.Parameters.Add(new OracleParameter("Dokument", request.DokumentBytes ));
        command.Parameters.Add(new OracleParameter("QrSingl", request.QrSinglBytes));
        command.Parameters.Add(new OracleParameter("QrFull", request.QrFullBytes));
    }

    private static async Task UpdateOrInsertNalozi(List<NalogPostDto> requestListUpdate, OracleConnection connection, OracleTransaction transaction, string queryUpdate, string queryInsert)
    {
        try
        {
            foreach (var request in requestListUpdate)
            {
                using var commandUpdate = new OracleCommand(queryUpdate, connection);
                commandUpdate.Transaction = transaction;

                AddParametersToCommand(commandUpdate, request);

                int rowsAffected = await commandUpdate.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    using var commandInsert = new OracleCommand(queryInsert, connection);
                    commandInsert.Transaction = transaction;

                    AddParametersToCommand(commandInsert, request);

                    await commandInsert.ExecuteNonQueryAsync();
                }
            }
        }
        catch (OracleException ex)
        {
            Console.WriteLine($"Error: {ex.Message}, Code: {ex.Number}");
            throw;
        }
    }

    private async Task<byte[]?> ExecuteQueryAsync(string query, params OracleParameter[] parameters)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(query, connection);
        command.Parameters.AddRange(parameters);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        if (await reader.ReadAsync())
        {
            return reader["DOKUMENT"] as byte[];
        }

        return null;
    }
}
