using Backend.Models;
using Npgsql;

namespace Backend.Services;

/// <summary>
/// Creates the candidate tables (one per CV section) and fills them with made-up people, once.
/// The names, companies and schools are invented; no real person's details are stored here.
/// </summary>
public static class CandidateStore
{
    private static readonly string[] FirstNames = { "Noa", "Daniel", "Maya", "Yossi", "Lina", "Omer", "Tamar", "Itai", "Shira", "Eli" };
    private static readonly string[] LastNames = { "Ben-David", "Shapira", "Levi", "Katz", "Golan", "Mizrahi", "Friedman", "Adler", "Peretz", "Sasson" };
    private static readonly string[] Cities = { "Tel Aviv", "Haifa", "Jerusalem", "Melbourne", "Beer Sheva", "Herzliya" };
    private static readonly string[] Headlines = { "Backend engineer", "Full-stack developer", "Software architect", "Support engineering lead", "Product manager", "Data engineer" };
    private static readonly string[] Companies = { "Blue Harbor Software", "Cedar Peak Labs", "Nimbus Works", "Orchard Data", "Summit Retail Tech", "Lighthouse Systems", "Quarry Analytics" };
    private static readonly string[] Roles = { "Senior Developer", "Team Lead", "Solutions Engineer", "Product Manager", "Support Engineer", "Software Architect" };
    private static readonly string[] Skills = { "C# / .NET", "ASP.NET Core", "PostgreSQL", "SQL Server", "React", "TypeScript", "Azure", "Docker", "REST APIs", "Entity Framework Core", "Python", "Project management", "Pre-sales", "CI/CD" };
    private static readonly string[] Schools = { "Open University", "Tel Aviv University", "Technion", "Monash University", "Hebrew University" };
    private static readonly string[] Degrees = { "BA, Computer Science", "BSc, Software Engineering", "BA, Management" };
    private static readonly string[] Certifications = { "Azure Fundamentals", "Oracle Certified Professional", "Microsoft Certified Application Developer", "Scrum Master" };

    /// <summary>Creates the tables if they are missing, and adds 10 made-up candidates if the candidates table is empty.</summary>
    public static async Task EnsureSampleDataAsync(string connectionString)
    {
        await using var conn = await Database.OpenAsync(connectionString);

        await using (var create = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS candidates (
                id serial PRIMARY KEY,
                full_name text NOT NULL,
                headline text NOT NULL,
                city text NOT NULL,
                email text NOT NULL
            );
            CREATE TABLE IF NOT EXISTS candidate_experiences (
                id serial PRIMARY KEY,
                candidate_id integer NOT NULL REFERENCES candidates(id),
                company text NOT NULL,
                role text NOT NULL,
                start_year integer NOT NULL,
                end_year integer,
                summary text NOT NULL
            );
            CREATE TABLE IF NOT EXISTS candidate_skills (
                id serial PRIMARY KEY,
                candidate_id integer NOT NULL REFERENCES candidates(id),
                name text NOT NULL
            );
            CREATE TABLE IF NOT EXISTS candidate_education (
                id serial PRIMARY KEY,
                candidate_id integer NOT NULL REFERENCES candidates(id),
                school text NOT NULL,
                degree text NOT NULL,
                year integer NOT NULL
            );
            CREATE TABLE IF NOT EXISTS candidate_certifications (
                id serial PRIMARY KEY,
                candidate_id integer NOT NULL REFERENCES candidates(id),
                name text NOT NULL
            );", conn))
        {
            await create.ExecuteNonQueryAsync();
        }

        await using (var count = new NpgsqlCommand("SELECT COUNT(*) FROM candidates;", conn))
        {
            if (Convert.ToInt64(await count.ExecuteScalarAsync()) > 0) return;
        }

        // A fixed seed means the made-up rows are the same every time
        var random = new Random(7);
        await using var tx = await conn.BeginTransactionAsync();
        for (var i = 0; i < 10; i++)
        {
            var first = Pick(FirstNames, random);
            var last = Pick(LastNames, random);

            await using var insertCandidate = new NpgsqlCommand(
                "INSERT INTO candidates (full_name, headline, city, email) VALUES (@name, @headline, @city, @email) RETURNING id;", conn, tx);
            insertCandidate.Parameters.AddWithValue("name", $"{first} {last}");
            insertCandidate.Parameters.AddWithValue("headline", Pick(Headlines, random));
            insertCandidate.Parameters.AddWithValue("city", Pick(Cities, random));
            insertCandidate.Parameters.AddWithValue("email", $"{first}.{last}@example.com".ToLowerInvariant());
            var candidateId = Convert.ToInt32(await insertCandidate.ExecuteScalarAsync());

            // Two or three jobs, oldest first; the last one has no end year (still current)
            var jobs = random.Next(2, 4);
            var startYear = 2024 - random.Next(12, 25);
            for (var j = 0; j < jobs; j++)
            {
                var endYear = j == jobs - 1 ? (int?)null : startYear + 2 + random.Next(0, 3);
                await using var insertJob = new NpgsqlCommand(
                    "INSERT INTO candidate_experiences (candidate_id, company, role, start_year, end_year, summary) VALUES (@id, @company, @role, @start, @end, @summary);", conn, tx);
                insertJob.Parameters.AddWithValue("id", candidateId);
                insertJob.Parameters.AddWithValue("company", Pick(Companies, random));
                insertJob.Parameters.AddWithValue("role", Pick(Roles, random));
                insertJob.Parameters.AddWithValue("start", startYear);
                insertJob.Parameters.AddWithValue("end", (object?)endYear ?? DBNull.Value);
                insertJob.Parameters.AddWithValue("summary", $"Worked on {Pick(Skills, random)} projects with a team of {random.Next(3, 12)}.");
                await insertJob.ExecuteNonQueryAsync();
                startYear = endYear ?? startYear;
            }

            foreach (var skill in Skills.OrderBy(_ => random.Next()).Take(random.Next(4, 8)))
            {
                await using var insertSkill = new NpgsqlCommand("INSERT INTO candidate_skills (candidate_id, name) VALUES (@id, @name);", conn, tx);
                insertSkill.Parameters.AddWithValue("id", candidateId);
                insertSkill.Parameters.AddWithValue("name", skill);
                await insertSkill.ExecuteNonQueryAsync();
            }

            await using var insertEducation = new NpgsqlCommand(
                "INSERT INTO candidate_education (candidate_id, school, degree, year) VALUES (@id, @school, @degree, @year);", conn, tx);
            insertEducation.Parameters.AddWithValue("id", candidateId);
            insertEducation.Parameters.AddWithValue("school", Pick(Schools, random));
            insertEducation.Parameters.AddWithValue("degree", Pick(Degrees, random));
            insertEducation.Parameters.AddWithValue("year", 2000 + random.Next(0, 20));
            await insertEducation.ExecuteNonQueryAsync();

            foreach (var certification in Certifications.OrderBy(_ => random.Next()).Take(random.Next(0, 3)))
            {
                await using var insertCertification = new NpgsqlCommand("INSERT INTO candidate_certifications (candidate_id, name) VALUES (@id, @name);", conn, tx);
                insertCertification.Parameters.AddWithValue("id", candidateId);
                insertCertification.Parameters.AddWithValue("name", certification);
                await insertCertification.ExecuteNonQueryAsync();
            }
        }
        await tx.CommitAsync();
    }

    /// <summary>Returns the made-up candidates, in the order they were added.</summary>
    public static async Task<List<Candidate>> GetAllAsync(string connectionString)
    {
        var candidates = new List<Candidate>();
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand("SELECT id, full_name, headline, city, email FROM candidates ORDER BY id;", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            candidates.Add(new Candidate
            {
                Id = reader.GetInt32(0),
                FullName = reader.GetString(1),
                Headline = reader.GetString(2),
                City = reader.GetString(3),
                Email = reader.GetString(4)
            });
        }
        return candidates;
    }

    private static string Pick(string[] items, Random random) => items[random.Next(items.Length)];
}
