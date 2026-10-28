using System.Data;
using Microsoft.Data.SqlClient;
using TTLooks.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<DatabaseHelper>();
builder.Services.AddSingleton<AiService>();
builder.Services.AddCors();

var app = builder.Build();

app.UseCors(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseDefaultFiles();
app.UseStaticFiles();

// ==========================================
// 1. API CÔNG KHAI: DANH SÁCH VÉ & CHI TIẾT
// ==========================================
app.MapGet("/api/activities", (string? search, DatabaseHelper db) =>
{
    try
    {
        string sql = "SELECT ActivityId, Title, Description, Price, ImageUrl FROM Activities WHERE IsActive = 1";
        var parameters = new List<SqlParameter>();
        if (!string.IsNullOrEmpty(search))
        {
            sql += " AND (Title LIKE @Search OR Description LIKE @Search)";
            parameters.Add(new SqlParameter("@Search", $"%{search}%"));
        }
        sql += " ORDER BY ActivityId DESC";

        DataTable dt = db.ExecuteQuery(sql, parameters.Count > 0 ? parameters.ToArray() : null);
        var list = new List<object>();
        foreach (DataRow r in dt.Rows)
        {
            list.Add(new
            {
                ActivityId = r["ActivityId"],
                Title = r["Title"].ToString(),
                Description = r["Description"].ToString(),
                Price = Convert.ToDecimal(r["Price"]),
                ImageUrl = r["ImageUrl"].ToString()
            });
        }
        return Results.Ok(list);
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi đọc CSDL: " + ex.Message);
    }
});

app.MapGet("/api/activities/{id:int}", (int id, DatabaseHelper db) =>
{
    try
    {
        string sql = "SELECT TOP 1 ActivityId, Title, Description, Price, ImageUrl FROM Activities WHERE ActivityId = @Id";
        DataTable dt = db.ExecuteQuery(sql, new[] { new SqlParameter("@Id", id) });
        if (dt.Rows.Count == 0) return Results.NotFound(new { message = "Không tìm thấy trải nghiệm này." });

        var r = dt.Rows[0];
        return Results.Ok(new
        {
            activityId = r["ActivityId"],
            title = r["Title"].ToString(),
            description = r["Description"].ToString(),
            price = Convert.ToDecimal(r["Price"]),
            imageUrl = r["ImageUrl"].ToString()
        });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi đọc chi tiết: " + ex.Message);
    }
});

// ==========================================
// 2. API XÁC THỰC: ĐĂNG KÝ & ĐĂNG NHẬP
// ==========================================
app.MapPost("/api/auth/register", (RegisterRequest req, DatabaseHelper db) =>
{
    try
    {
        string checkSql = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
        DataTable dtCheck = db.ExecuteQuery(checkSql, new[] { new SqlParameter("@Email", req.Email) });
        if (Convert.ToInt32(dtCheck.Rows[0][0]) > 0)
        {
            return Results.BadRequest(new { message = "Email này đã được sử dụng!" });
        }

        string insertSql = @"
            INSERT INTO Users (FullName, Email, PasswordHash, PhoneNumber, RoleId)
            VALUES (@FullName, @Email, @Password, @Phone, 2)";
        SqlParameter[] pars = {
            new SqlParameter("@FullName", req.FullName),
            new SqlParameter("@Email", req.Email),
            new SqlParameter("@Password", req.Password),
            new SqlParameter("@Phone", req.Phone ?? "")
        };
        db.ExecuteNonQuery(insertSql, pars);
        return Results.Ok(new { message = "Đăng ký thành công! Vui lòng đăng nhập." });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi đăng ký: " + ex.Message);
    }
});

app.MapPost("/api/auth/login", (LoginRequest req, DatabaseHelper db) =>
{
    try
    {
        string query = "SELECT UserId, FullName, Email, PhoneNumber, RoleId FROM Users WHERE Email = @Email AND PasswordHash = @Password";
        SqlParameter[] pars = {
            new SqlParameter("@Email", req.Email),
            new SqlParameter("@Password", req.Password)
        };
        DataTable dt = db.ExecuteQuery(query, pars);
        if (dt.Rows.Count > 0)
        {
            var r = dt.Rows[0];
            return Results.Ok(new
            {
                userId = r["UserId"],
                fullName = r["FullName"].ToString(),
                email = r["Email"].ToString(),
                phoneNumber = r["PhoneNumber"]?.ToString() ?? "",
                roleId = Convert.ToInt32(r["RoleId"])
            });
        }
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi đăng nhập: " + ex.Message);
    }
});

// ==========================================
// 3. API HỒ SƠ CÁ NHÂN & ĐỔI MẬT KHẨU
// ==========================================
app.MapGet("/api/user/profile/{userId:int}", (int userId, DatabaseHelper db) =>
{
    try
    {
        string query = "SELECT UserId, FullName, Email, PhoneNumber, CreatedAt FROM Users WHERE UserId = @UserId";
        DataTable dt = db.ExecuteQuery(query, new[] { new SqlParameter("@UserId", userId) });
        if (dt.Rows.Count == 0) return Results.NotFound(new { message = "Không tìm thấy người dùng." });

        var r = dt.Rows[0];
        return Results.Ok(new
        {
            userId = r["UserId"],
            fullName = r["FullName"].ToString(),
            email = r["Email"].ToString(),
            phoneNumber = r["PhoneNumber"]?.ToString() ?? "",
            createdAt = Convert.ToDateTime(r["CreatedAt"]).ToString("dd/MM/yyyy")
        });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi đọc hồ sơ: " + ex.Message);
    }
});

app.MapPut("/api/user/profile/{userId:int}", (UpdateProfileRequest req, int userId, DatabaseHelper db) =>
{
    try
    {
        string sql = "UPDATE Users SET FullName = @FullName, PhoneNumber = @Phone WHERE UserId = @UserId";
        SqlParameter[] pars = {
            new SqlParameter("@FullName", req.FullName),
            new SqlParameter("@Phone", req.PhoneNumber ?? ""),
            new SqlParameter("@UserId", userId)
        };
        db.ExecuteNonQuery(sql, pars);
        return Results.Ok(new { message = "Cập nhật hồ sơ thành công!" });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi cập nhật: " + ex.Message);
    }
});

app.MapPut("/api/user/change-password/{userId:int}", (ChangePasswordRequest req, int userId, DatabaseHelper db) =>
{
    try
    {
        string checkSql = "SELECT COUNT(1) FROM Users WHERE UserId = @UserId AND PasswordHash = @OldPassword";
        SqlParameter[] checkPars = {
            new SqlParameter("@UserId", userId),
            new SqlParameter("@OldPassword", req.OldPassword)
        };
        DataTable dt = db.ExecuteQuery(checkSql, checkPars);
        if (Convert.ToInt32(dt.Rows[0][0]) == 0)
        {
            return Results.BadRequest(new { message = "Mật khẩu hiện tại không chính xác!" });
        }

        string updateSql = "UPDATE Users SET PasswordHash = @NewPassword WHERE UserId = @UserId";
        SqlParameter[] updatePars = {
            new SqlParameter("@NewPassword", req.NewPassword),
            new SqlParameter("@UserId", userId)
        };
        db.ExecuteNonQuery(updateSql, updatePars);
        return Results.Ok(new { message = "Đổi mật khẩu thành công!" });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi đổi mật khẩu: " + ex.Message);
    }
});

// ==========================================
// 4. API ĐẶT VÉ & LỊCH SỬ KHÁCH HÀNG
// ==========================================
app.MapPost("/api/bookings/create", (CreateBookingRequest req, DatabaseHelper db) =>
{
    try
    {
        string ticketCode = "TTL-" + DateTime.Now.Year + "-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        string insertSql = @"
            INSERT INTO Bookings (UserId, ActivityId, CustomerName, CustomerEmail, CustomerPhone, TravelDate, Quantity, TotalPrice, Status, TicketCode)
            OUTPUT INSERTED.BookingId
            VALUES (@UserId, @ActivityId, @CustomerName, @CustomerEmail, @CustomerPhone, @TravelDate, @Quantity, @TotalPrice, N'Chờ thanh toán', @TicketCode)";

        SqlParameter[] pars = {
            new SqlParameter("@UserId", (object?)req.UserId ?? DBNull.Value),
            new SqlParameter("@ActivityId", req.ActivityId),
            new SqlParameter("@CustomerName", req.CustomerName),
            new SqlParameter("@CustomerEmail", req.CustomerEmail),
            new SqlParameter("@CustomerPhone", req.CustomerPhone),
            new SqlParameter("@TravelDate", req.TravelDate),
            new SqlParameter("@Quantity", req.Quantity),
            new SqlParameter("@TotalPrice", req.TotalPrice),
            new SqlParameter("@TicketCode", ticketCode)
        };

        DataTable dt = db.ExecuteQuery(insertSql, pars);
        int bookingId = Convert.ToInt32(dt.Rows[0]["BookingId"]);

        string qrContent = $"TTLOOKS BOOKING {bookingId}";
        string vietQrUrl = $"https://img.vietqr.io/image/MB-0934968020-compact2.png?amount={(long)req.TotalPrice}&addInfo={Uri.EscapeDataString(qrContent)}&accountName=LE%20QUOC%20TU";

        return Results.Ok(new
        {
            bookingId,
            ticketCode,
            totalPrice = req.TotalPrice,
            vietQrUrl,
            qrContent
        });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi tạo đơn đặt vé: " + ex.Message);
    }
});

app.MapPost("/api/bookings/confirm/{bookingId:int}", (int bookingId, DatabaseHelper db) =>
{
    try
    {
        string updateSql = "UPDATE Bookings SET Status = N'Đã thanh toán' WHERE BookingId = @Id";
        db.ExecuteNonQuery(updateSql, new[] { new SqlParameter("@Id", bookingId) });
        return Results.Ok(new { message = "Thanh toán thành công! Vé điện tử đã sẵn sàng." });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi xác nhận: " + ex.Message);
    }
});

app.MapGet("/api/bookings/my/{userId:int}", (int userId, DatabaseHelper db) =>
{
    try
    {
        string sql = @"
            SELECT b.BookingId, a.Title, a.ImageUrl, b.CustomerName, b.CustomerPhone, b.CustomerEmail, 
                   b.TravelDate, b.Quantity, b.TotalPrice, b.Status, b.TicketCode, b.CreatedAt
            FROM Bookings b
            INNER JOIN Activities a ON b.ActivityId = a.ActivityId
            WHERE b.UserId = @UserId
            ORDER BY b.BookingId DESC";

        DataTable dt = db.ExecuteQuery(sql, new[] { new SqlParameter("@UserId", userId) });
        var list = new List<object>();
        foreach (DataRow r in dt.Rows)
        {
            int bookingId = Convert.ToInt32(r["BookingId"]);
            decimal totalPrice = Convert.ToDecimal(r["TotalPrice"]);
            string qrContent = $"TTLOOKS BOOKING {bookingId}";
            string vietQrUrl = $"https://img.vietqr.io/image/MB-0934968020-compact2.png?amount={(long)totalPrice}&addInfo={Uri.EscapeDataString(qrContent)}&accountName=LE%20QUOC%20TU";

            list.Add(new
            {
                bookingId,
                title = r["Title"].ToString(),
                imageUrl = r["ImageUrl"].ToString(),
                customerName = r["CustomerName"].ToString(),
                customerPhone = r["CustomerPhone"].ToString(),
                customerEmail = r["CustomerEmail"].ToString(),
                travelDate = Convert.ToDateTime(r["TravelDate"]).ToString("dd/MM/yyyy"),
                quantity = r["Quantity"],
                totalPrice,
                status = r["Status"].ToString(),
                ticketCode = r["TicketCode"].ToString(),
                vietQrUrl,
                qrContent,
                createdAt = Convert.ToDateTime(r["CreatedAt"]).ToString("dd/MM/yyyy HH:mm")
            });
        }
        return Results.Ok(list);
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi đọc lịch sử vé: " + ex.Message);
    }
});

// ==========================================
// 5. API DÀNH RIÊNG CHO ADMIN QUẢN TRỊ
// ==========================================

// 5.1 Thống kê Dashboard KPI
app.MapGet("/api/admin/stats", (DatabaseHelper db) =>
{
    try
    {
        decimal revenue = 0;
        int paidTickets = 0;
        int totalUsers = 0;
        int totalActivities = 0;

        DataTable dtRev = db.ExecuteQuery("SELECT ISNULL(SUM(TotalPrice), 0) AS Revenue, ISNULL(SUM(Quantity), 0) AS Tickets FROM Bookings WHERE Status = N'Đã thanh toán'");
        if (dtRev.Rows.Count > 0)
        {
            revenue = Convert.ToDecimal(dtRev.Rows[0]["Revenue"]);
            paidTickets = Convert.ToInt32(dtRev.Rows[0]["Tickets"]);
        }

        DataTable dtU = db.ExecuteQuery("SELECT COUNT(1) FROM Users");
        if (dtU.Rows.Count > 0) totalUsers = Convert.ToInt32(dtU.Rows[0][0]);

        DataTable dtA = db.ExecuteQuery("SELECT COUNT(1) FROM Activities");
        if (dtA.Rows.Count > 0) totalActivities = Convert.ToInt32(dtA.Rows[0][0]);

        return Results.Ok(new { revenue, paidTickets, totalUsers, totalActivities });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi thống kê: " + ex.Message);
    }
});

// 5.2 Quản lý Đơn vé (Xem toàn bộ, Duyệt đơn, Hủy đơn)
app.MapGet("/api/admin/bookings", (DatabaseHelper db) =>
{
    try
    {
        string sql = @"
            SELECT b.BookingId, b.TicketCode, b.CustomerName, b.CustomerEmail, b.CustomerPhone,
                   a.Title AS ActivityTitle, b.TravelDate, b.Quantity, b.TotalPrice, b.Status, b.CreatedAt
            FROM Bookings b
            INNER JOIN Activities a ON b.ActivityId = a.ActivityId
            ORDER BY b.BookingId DESC";

        DataTable dt = db.ExecuteQuery(sql);
        var list = new List<object>();
        foreach (DataRow r in dt.Rows)
        {
            list.Add(new
            {
                bookingId = r["BookingId"],
                ticketCode = r["TicketCode"].ToString(),
                customerName = r["CustomerName"].ToString(),
                customerEmail = r["CustomerEmail"].ToString(),
                customerPhone = r["CustomerPhone"].ToString(),
                activityTitle = r["ActivityTitle"].ToString(),
                travelDate = Convert.ToDateTime(r["TravelDate"]).ToString("dd/MM/yyyy"),
                quantity = r["Quantity"],
                totalPrice = Convert.ToDecimal(r["TotalPrice"]),
                status = r["Status"].ToString(),
                createdAt = Convert.ToDateTime(r["CreatedAt"]).ToString("dd/MM/yyyy HH:mm")
            });
        }
        return Results.Ok(list);
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi nạp đơn: " + ex.Message);
    }
});

app.MapPut("/api/admin/bookings/{id:int}/status", (UpdateStatusRequest req, int id, DatabaseHelper db) =>
{
    try
    {
        string sql = "UPDATE Bookings SET Status = @Status WHERE BookingId = @Id";
        SqlParameter[] pars = {
            new SqlParameter("@Status", req.Status),
            new SqlParameter("@Id", id)
        };
        db.ExecuteNonQuery(sql, pars);
        return Results.Ok(new { message = $"Đã cập nhật trạng thái đơn #{id} thành '{req.Status}'." });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi cập nhật: " + ex.Message);
    }
});

// 5.3 Quản lý Vé & Dịch vụ (Xem toàn bộ, Thêm mới, Xóa)
app.MapGet("/api/admin/activities", (DatabaseHelper db) =>
{
    try
    {
        string sql = "SELECT ActivityId, Title, Description, Price, ImageUrl, IsActive FROM Activities ORDER BY ActivityId DESC";
        DataTable dt = db.ExecuteQuery(sql);
        var list = new List<object>();
        foreach (DataRow r in dt.Rows)
        {
            list.Add(new
            {
                activityId = r["ActivityId"],
                title = r["Title"].ToString(),
                description = r["Description"].ToString(),
                price = Convert.ToDecimal(r["Price"]),
                imageUrl = r["ImageUrl"].ToString(),
                isActive = Convert.ToBoolean(r["IsActive"])
            });
        }
        return Results.Ok(list);
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi đọc danh sách vé: " + ex.Message);
    }
});

app.MapPost("/api/admin/activities", (CreateActivityRequest req, DatabaseHelper db) =>
{
    try
    {
        string sql = @"
            INSERT INTO Activities (Title, Description, Price, ImageUrl, IsActive)
            VALUES (@Title, @Description, @Price, @ImageUrl, 1)";
        SqlParameter[] pars = {
            new SqlParameter("@Title", req.Title),
            new SqlParameter("@Description", req.Description ?? ""),
            new SqlParameter("@Price", req.Price),
            new SqlParameter("@ImageUrl", string.IsNullOrEmpty(req.ImageUrl) ? "https://images.unsplash.com/photo-1506744038136-46273834b3fb" : req.ImageUrl)
        };
        db.ExecuteNonQuery(sql, pars);
        return Results.Ok(new { message = "Thêm trải nghiệm mới thành công!" });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi thêm vé: " + ex.Message);
    }
});

app.MapDelete("/api/admin/activities/{id:int}", (int id, DatabaseHelper db) =>
{
    try
    {
        // Ẩn thay vì xóa cứng nếu đã có người đặt
        string checkSql = "SELECT COUNT(1) FROM Bookings WHERE ActivityId = @Id";
        DataTable dt = db.ExecuteQuery(checkSql, new[] { new SqlParameter("@Id", id) });
        if (Convert.ToInt32(dt.Rows[0][0]) > 0)
        {
            db.ExecuteNonQuery("UPDATE Activities SET IsActive = 0 WHERE ActivityId = @Id", new[] { new SqlParameter("@Id", id) });
            return Results.Ok(new { message = "Vé này đã có người đặt nên hệ thống đã chuyển sang trạng thái Ẩn." });
        }

        db.ExecuteNonQuery("DELETE FROM Activities WHERE ActivityId = @Id", new[] { new SqlParameter("@Id", id) });
        return Results.Ok(new { message = "Đã xóa trải nghiệm thành công!" });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi xóa vé: " + ex.Message);
    }
});

// 5.4 Quản lý Người dùng (Xem danh sách, Nâng/Hạ quyền, Xóa)
app.MapGet("/api/admin/users", (DatabaseHelper db) =>
{
    try
    {
        string sql = "SELECT UserId, FullName, Email, PhoneNumber, RoleId, CreatedAt FROM Users ORDER BY UserId ASC";
        DataTable dt = db.ExecuteQuery(sql);
        var list = new List<object>();
        foreach (DataRow r in dt.Rows)
        {
            list.Add(new
            {
                userId = r["UserId"],
                fullName = r["FullName"].ToString(),
                email = r["Email"].ToString(),
                phoneNumber = r["PhoneNumber"]?.ToString() ?? "",
                roleId = Convert.ToInt32(r["RoleId"]),
                createdAt = Convert.ToDateTime(r["CreatedAt"]).ToString("dd/MM/yyyy HH:mm")
            });
        }
        return Results.Ok(list);
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi nạp người dùng: " + ex.Message);
    }
});

app.MapPut("/api/admin/users/{id:int}/role", (UpdateRoleRequest req, int id, DatabaseHelper db) =>
{
    try
    {
        string sql = "UPDATE Users SET RoleId = @RoleId WHERE UserId = @Id";
        SqlParameter[] pars = {
            new SqlParameter("@RoleId", req.RoleId),
            new SqlParameter("@Id", id)
        };
        db.ExecuteNonQuery(sql, pars);
        return Results.Ok(new { message = "Đã cập nhật vai trò người dùng thành công!" });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi đổi quyền: " + ex.Message);
    }
});

// ==========================================
// 6. API AI LẬP LỊCH & CHATBOT
// ==========================================
app.MapPost("/api/ai/planner", async (AiPlanRequest req, AiService ai) =>
{
    var plan = await ai.GenerateTravelPlanAsync(req.Destination, req.Days, req.Budget, req.Preferences);
    return Results.Ok(new { result = plan });
});

app.MapPost("/api/ai/planner-visual", async (AiPlanRequest req, AiService ai, DatabaseHelper db) =>
{
    try
    {
        string aiAdvice = await ai.GenerateTravelPlanAsync(req.Destination, req.Days, req.Budget, req.Preferences);

        string sql = "SELECT ActivityId, Title, Description, Price, ImageUrl FROM Activities WHERE IsActive = 1 AND (Title LIKE @Dest OR Description LIKE @Dest)";
        DataTable dt = db.ExecuteQuery(sql, new[] { new SqlParameter("@Dest", $"%{req.Destination}%") });

        if (dt.Rows.Count == 0)
        {
            dt = db.ExecuteQuery("SELECT TOP 6 ActivityId, Title, Description, Price, ImageUrl FROM Activities WHERE IsActive = 1 ORDER BY ActivityId DESC");
        }

        var activities = new List<object>();
        foreach (DataRow r in dt.Rows)
        {
            activities.Add(new
            {
                activityId = r["ActivityId"],
                title = r["Title"].ToString(),
                description = r["Description"].ToString(),
                price = Convert.ToDecimal(r["Price"]),
                imageUrl = r["ImageUrl"].ToString()
            });
        }

        return Results.Ok(new
        {
            advice = aiAdvice,
            destination = req.Destination,
            days = req.Days,
            activities
        });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi tạo lịch trình: " + ex.Message);
    }
});

app.MapPost("/api/ai/chat", async (ChatHistoryRequest req, AiService ai) =>
{
    var reply = await ai.AskTravelChatbotWithHistoryAsync(req.History ?? new List<ChatTurnDto>(), req.Message);
    return Results.Ok(new { reply });
});

app.Run();

// DTO Models
public record RegisterRequest(string FullName, string Email, string Password, string? Phone);
public record LoginRequest(string Email, string Password);
public record UpdateProfileRequest(string FullName, string? PhoneNumber);
public record ChangePasswordRequest(string OldPassword, string NewPassword);
public record CreateBookingRequest(int? UserId, int ActivityId, string CustomerName, string CustomerEmail, string CustomerPhone, string TravelDate, int Quantity, decimal TotalPrice);
public record AiPlanRequest(string Destination, string Days, string Budget, string Preferences);
public record ChatHistoryRequest(string Message, List<ChatTurnDto>? History);
public record UpdateStatusRequest(string Status);
public record CreateActivityRequest(string Title, string? Description, decimal Price, string? ImageUrl);
public record UpdateRoleRequest(int RoleId);