using System.Text.Json;        // 씨앗 데이터(Data/monsters.json)를 읽을 때만 쓴다
using Microsoft.Data.Sqlite;   // ← 추가: SQLite 데이터베이스를 여는 도구

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ── 2회차. DB 파일 준비 ────────────────────────────────────────────────
// Data/game.db 파일 하나가 데이터베이스다. 없으면 처음 열 때 만들어진다
// var dbPath = Path.Combine(app.Environment.ContentRootPath, "Data", "game.db");
// var connectionString = $"Data Source={dbPath}";

// Data 폴더 경로
var dataFolder = Path.Combine(
    app.Environment.ContentRootPath, "Data");

// 폴더가 없으면 자동 생성
Directory.CreateDirectory(dataFolder);

// SQLite DB 경로
var dbPath = Path.Combine(dataFolder, "game.db");

var connectionString = $"Data Source={dbPath}";

// 테이블이 없으면 만든다. 있으면 그대로 둔다 (IF NOT EXISTS)
using (var conn = new SqliteConnection(connectionString))
{
    conn.Open();
    var create = conn.CreateCommand(); 
    create.CommandText = """
        CREATE TABLE IF NOT EXISTS monsters (
            id     INTEGER PRIMARY KEY AUTOINCREMENT,
            name   TEXT    NOT NULL,
            hp     INTEGER NOT NULL,
            attack INTEGER NOT NULL,
            speed  REAL    NOT NULL
        )
        """;
    create.ExecuteNonQuery();

    // 테이블이 비어 있을 때만 1회차의 monsters.json을 초기 데이터로 넣는다. 그래서 재시작해도 중복되지 않는다
    var count = conn.CreateCommand();
    count.CommandText = "SELECT COUNT(*) FROM monsters";
    if ((long)count.ExecuteScalar()! == 0)
    {
        var seedPath = Path.Combine(app.Environment.ContentRootPath, "Data", "monsters.json");
        var seed = JsonSerializer.Deserialize<List<Monster>>(
            File.ReadAllText(seedPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        foreach (var m in seed)
        {
            var insert = conn.CreateCommand();
            insert.CommandText = "INSERT INTO monsters (name, hp, attack, speed) VALUES ($name, $hp, $attack, $speed)";
            insert.Parameters.AddWithValue("$name", m.Name);
            insert.Parameters.AddWithValue("$hp", m.Hp);
            insert.Parameters.AddWithValue("$attack", m.Attack);
            insert.Parameters.AddWithValue("$speed", m.Speed);
            insert.ExecuteNonQuery();
        }
    }


// ── 랭킹 테이블 준비 ────────────────────────────────────────────────
    var rangkingcreate = conn.CreateCommand();

rangkingcreate.CommandText = """
    CREATE TABLE IF NOT EXISTS rangking (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        player_id TEXT,
        name TEXT NOT NULL,
        usescore INTEGER NOT NULL DEFAULT 0
    )
    """;

rangkingcreate.ExecuteNonQuery();

    // ── 랭킹 초기 데이터 등록 ───────────────────────────

        // DB에 저장된 랭킹 데이터 개수 확인
    var rangkingcount = conn.CreateCommand();

    rangkingcount.CommandText =
        "SELECT COUNT(*) FROM rangking";

    // 현재 저장된 랭킹 개수
    long countRanking = (long)rangkingcount.ExecuteScalar()!;

    // 랭킹 데이터가 없을 경우
    if (countRanking == 0)
    {
        Console.WriteLine("랭킹 데이터가 없습니다.");
        Console.WriteLine("새로운 랭킹 데이터를 등록할 수 있습니다.");
    }
    else
    {
        Console.WriteLine($"현재 랭킹 데이터: {countRanking}명");
    }
// ── 랭킹 테이블 준비 ────────────────────────────────────────────────
}

// SELECT 결과(행들)를 Monster 목록으로 바꾸는 헬퍼. GET 네 곳에서 같이 쓴다
static List<Monster> ReadMonsters(SqliteDataReader reader)
{
    var list = new List<Monster>();
    while (reader.Read())   // 한 줄(한 마리)씩
    {
        list.Add(new Monster
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Hp = reader.GetInt32(2),
            Attack = reader.GetInt32(3),
            Speed = reader.GetFloat(4)
        });
    }
    return list;
}

// 모든 몬스터를 DB에서 읽는다. /monsters, /monsters/slow, /monsters/big이 같이 쓴다
static List<Monster> LoadAllMonsters(string connectionString)
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, name, hp, attack, speed FROM monsters ORDER BY id";
    using var reader = cmd.ExecuteReader();
    return ReadMonsters(reader);
}

// ── 랭킹 ────────────────────────────────────────────────

static List<Rangking> ReadRangkings(SqliteDataReader reader)
{
    var rklist = new List<Rangking>();
    while (reader.Read())   // 한 줄(한 마리)씩
    {
        rklist.Add(new Rangking
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Usescore = reader.GetInt32(2)
        });
    }
    return rklist;
}

static List<Rangking> LoadAllRangkings(string connectionString)
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var rkcmd = conn.CreateCommand();
    rkcmd.CommandText =
    "SELECT id, name, usescore FROM rangking " +
    "WHERE id = $id";
    using var rkreader = rkcmd.ExecuteReader();
    return ReadRangkings(rkreader);
}

// ── 랭킹 ────────────────────────────────────────────────


app.MapGet("/hello", () => "안녕하세요, 서버입니다.");

// ── 몬스터 조회 — 이제 파일이 아니라 DB에서 읽는다 ─────────────────────
app.MapGet("/monsters", () => new { monsters = LoadAllMonsters(connectionString) });



app.MapGet("/rangking", () => new { rangking = LoadAllRangkings(connectionString) });


/*// 몬스터 하나 — 없으면 404
app.MapGet("/monsters/{id:int}", (int id) =>
{
    var found = monsters.Find(m => m.Id == id);
    return found is null
        ? Results.NotFound(new { error = $"id {id} 몬스터가 없습니다." })
        : Results.Ok(found);
});*/

// ── 테스트용 1: 일부러 느린 서버 (기본 3초). 04 파트에서 기다리는 동안 Unity가 멈추는지 본다 ──
app.MapGet("/monsters/slow", async (int ms = 3000) =>
{
    await Task.Delay(ms);   // ms 밀리초 기다린다. ?ms=8000처럼 바꿀 수 있다
    return new { monsters = LoadAllMonsters(connectionString) };
});

// ── 테스트용 2: 일부러 큰 데이터 (기본 20,000마리). 04 파트에서 큰 JSON을 받을 때 걸리는 시간을 본다 ──
app.MapGet("/monsters/big", (int count = 20000) =>
{
    var monsters = LoadAllMonsters(connectionString);
    var big = new List<Monster>(count);
    for (int i = 1; i <= count; i++)
    {
        var src = monsters[(i - 1) % monsters.Count];   // 다섯 마리를 돌려 가며 복제한다
        big.Add(new Monster { Id = i, Name = $"{src.Name} #{i}", Hp = src.Hp, Attack = src.Attack, Speed = src.Speed });
    }
    return new { monsters = big };
});

app.MapGet("/monsters/{id:int}", (int id) =>
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, name, hp, attack, speed FROM monsters WHERE id = $id";
    cmd.Parameters.AddWithValue("$id", id);   // 값은 글자로 이어 붙이지 않고 파라미터로 넘긴다
    using var reader = cmd.ExecuteReader();
    var found = ReadMonsters(reader);
    return found.Count == 0
        ? Results.NotFound(new { error = $"id {id} 몬스터가 없습니다." })
        : Results.Ok(found[0]);
});

//

app.MapGet("/rangking/{id:int}", (int id) =>
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var rkcmd = conn.CreateCommand();
    rkcmd.CommandText =
    "SELECT id, name, usescore FROM rangking " +
    "WHERE id = $id";
    rkcmd.Parameters.AddWithValue("$id", id);   // 값은 글자로 이어 붙이지 않고 파라미터로 넘긴다
    using var rkreader = rkcmd.ExecuteReader();
    var found = ReadRangkings(rkreader);
    return found.Count == 0
        ? Results.NotFound(new { error = $"id {id} 랭킹이 없습니다." })
        : Results.Ok(found[0]);
});


app.MapGet("/rangking/top10", () =>
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var rkcmd = conn.CreateCommand();
    rkcmd.CommandText =
    "SELECT id, name, usescore FROM rangking " +
    "WHERE id = $id";
    using var rkreader = rkcmd.ExecuteReader();
    var top10list = ReadRangkings(rkreader);
    return Results.Ok(new { rangking = top10list });
});

//

// ── 2회차. 몬스터 추가 — POST는 "받아라". 본문의 JSON이 Monster로 들어온다 ──
app.MapPost("/monsters", (Monster m) =>
{
    if (string.IsNullOrWhiteSpace(m.Name))
        return Results.BadRequest(new { error = "name은 비어 있으면 안 됩니다." });

    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = """
        INSERT INTO monsters (name, hp, attack, speed) VALUES ($name, $hp, $attack, $speed);
        SELECT last_insert_rowid();
        """;
    cmd.Parameters.AddWithValue("$name", m.Name);
    cmd.Parameters.AddWithValue("$hp", m.Hp);
    cmd.Parameters.AddWithValue("$attack", m.Attack);
    cmd.Parameters.AddWithValue("$speed", m.Speed);
    m.Id = (int)(long)cmd.ExecuteScalar()!;   // DB가 붙여 준 새 id

    return Results.Created($"/monsters/{m.Id}", m);   // 201 Created + 새 몬스터
});

// ------- 랭킹 ----------

// ── 랭킹 점수 등록 및 자동 갱신 ─────────────────────────
app.MapPost("/rangking", (Rangking rk) =>
{
    // 1. 이름이 비어 있는지 검사
    if (string.IsNullOrWhiteSpace(rk.Name))
        return Results.BadRequest(new
        {
            error = "이름을 입력해야 합니다."
        });

    // 2. 음수 점수는 허용하지 않음
    if (rk.Usescore < 0)
        return Results.BadRequest(new
        {
            error = "점수는 0 이상이어야 합니다."
        });

    using var conn = new SqliteConnection(connectionString);
    conn.Open();

    // 여러 SQL 작업을 하나로 묶음
    using var transaction = conn.BeginTransaction();

    // 3. 같은 이름의 플레이어가 있는지 확인
    var check = conn.CreateCommand();
    check.Transaction = transaction;

    check.CommandText =
        "SELECT id, usescore FROM rangking " +
        "WHERE name = $name LIMIT 1";

    check.Parameters.AddWithValue("$name", rk.Name);

    int existingId = 0;
    int existingScore = 0;

    using (var reader = check.ExecuteReader())
    {
        if (reader.Read())
        {
            existingId = reader.GetInt32(0);
            existingScore = reader.GetInt32(1);
        }
    }

    // 4. 기존 플레이어인 경우
    if (existingId != 0)
    {
        // 기존 점수보다 높을 때만 수정
        if (rk.Usescore > existingScore)
        {
            var update = conn.CreateCommand();
            update.Transaction = transaction;

            update.CommandText =
                "UPDATE rangking SET usescore = $score " +
                "WHERE id = $id";

            update.Parameters.AddWithValue(
                "$score", rk.Usescore);

            update.Parameters.AddWithValue(
                "$id", existingId);

            update.ExecuteNonQuery();
        }

        transaction.Commit();

        return Results.Ok(new
        {
            id = existingId,
            name = rk.Name,
            usescore = Math.Max(existingScore, rk.Usescore),
            message = rk.Usescore > existingScore
                ? "최고 점수가 갱신되었습니다."
                : "기존 최고 점수를 유지합니다."
        });
    }

    // 5. 신규 플레이어라면 DB에 추가
    var insert = conn.CreateCommand();
    insert.Transaction = transaction;

    insert.CommandText = """
        INSERT INTO rangking (name, usescore)
        VALUES ($name, $score);
        SELECT last_insert_rowid();
        """;

    insert.Parameters.AddWithValue("$name", rk.Name);
    insert.Parameters.AddWithValue("$score", rk.Usescore);

    rk.Id = (int)(long)insert.ExecuteScalar()!;

    transaction.Commit();

    return Results.Created($"/rangking/{rk.Id}", new
    {
        id = rk.Id,
        name = rk.Name,
        usescore = rk.Usescore,
        message = "새로운 플레이어가 등록되었습니다."
    });
});

//


// ── 2회차. 몬스터 고치기 — PUT은 "이걸로 바꿔라". 본문의 JSON으로 그 id의 줄을 통째로 바꾼다 ──
app.MapPut("/monsters/{id:int}", (int id, Monster m) =>
{
    if (string.IsNullOrWhiteSpace(m.Name))
        return Results.BadRequest(new { error = "name은 비어 있으면 안 됩니다." });

    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE monsters SET name = $name, hp = $hp, attack = $attack, speed = $speed WHERE id = $id";
    cmd.Parameters.AddWithValue("$name", m.Name);
    cmd.Parameters.AddWithValue("$hp", m.Hp);
    cmd.Parameters.AddWithValue("$attack", m.Attack);
    cmd.Parameters.AddWithValue("$speed", m.Speed);
    cmd.Parameters.AddWithValue("$id", id);
    int changed = cmd.ExecuteNonQuery();   // 바뀐 줄 수. 그 id가 없으면 0

    if (changed == 0)
        return Results.NotFound(new { error = $"id {id} 몬스터가 없습니다." });

    m.Id = id;                 // 본문의 id는 무시하고 주소의 id를 쓴다
    return Results.Ok(m);      // 200 OK + 바뀐 몬스터
});


//

// ── 테스트용 랭킹 수정 ───────────────────────────────
app.MapPut("/rangking/{id:int}", (int id, Rangking rk) =>
{
    // 이름 검사
    if (string.IsNullOrWhiteSpace(rk.Name))
        return Results.BadRequest(new
        {
            error = "이름을 입력해야 합니다."
        });

    // 점수 검사
    if (rk.Usescore < 0)
        return Results.BadRequest(new
        {
            error = "점수는 0 이상이어야 합니다."
        });

    using var conn = new SqliteConnection(connectionString);
    conn.Open();

    var cmd = conn.CreateCommand();

    // 테스트용이므로 점수를 강제로 변경할 수 있음
    // 기존 점수보다 낮아도 수정 가능
    cmd.CommandText = """
        UPDATE rangking
        SET name = $name, usescore = $score
        WHERE id = $id
        """;

    cmd.Parameters.AddWithValue("$name", rk.Name);
    cmd.Parameters.AddWithValue("$score", rk.Usescore);
    cmd.Parameters.AddWithValue("$id", id);

    int changed = cmd.ExecuteNonQuery();

    // 해당 ID가 없으면 404 반환
    if (changed == 0)
        return Results.NotFound(new
        {
            error = "해당 랭킹이 없습니다."
        });

    rk.Id = id;

    return Results.Ok(rk);
});

//

// ── 2회차. 몬스터 삭제 — DELETE는 "지워라". 본문은 없고 주소의 id만 쓴다 ──
app.MapDelete("/monsters/{id:int}", (int id) =>
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "DELETE FROM monsters WHERE id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    int deleted = cmd.ExecuteNonQuery();   // 지운 줄 수

    return deleted == 0
        ? Results.NotFound(new { error = $"id {id} 몬스터가 없습니다." })
        : Results.NoContent();             // 204 No Content. 돌려줄 본문이 없다
});


//

// ── 테스트용 랭킹 삭제 ───────────────────────────────
app.MapDelete("/rangking/{id:int}", (int id) =>
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();

    var cmd = conn.CreateCommand();

    // 지정한 ID의 랭킹을 삭제
    cmd.CommandText =
        "DELETE FROM rangking WHERE id = $id";

    cmd.Parameters.AddWithValue("$id", id);

    int deleted = cmd.ExecuteNonQuery();

    // 삭제할 데이터가 없는 경우
    if (deleted == 0)
        return Results.NotFound(new
        {
            error = "해당 랭킹이 없습니다."
        });

    // 정상적으로 삭제한 경우
    return Results.NoContent();
});

//

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");

// 데이터 모양
class Monster
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Hp { get; set; }
    public int Attack { get; set; }
    public float Speed { get; set; }
}

// --------------------------------------------------

class Rangking
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Usescore { get; set; }
}

