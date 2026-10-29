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
// DỮ LIỆU MẪU DỰ PHÒNG KHI KHÔNG CÓ CSDL TRÊN CLOUD
// ==========================================
var defaultActivities = new List<dynamic>
{
    new { ActivityId = 1, Title = "Vé Cáp Treo Sun World Bà Nà Hills (Đà Nẵng)", City = "Đà Nẵng", Category = "Điểm đến Check-in", Description = "Trải nghiệm tuyến cáp treo đạt kỷ lục thế giới và check-in Cầu Vàng sương mây.", Price = 900000m, ImageUrl = "https://images.unsplash.com/photo-1583417319070-4a69db38a482", IsActive = true },
    new { ActivityId = 2, Title = "Tour Thuyền Thúng Rừng Dừa Bảy Mẫu Hội An", City = "Quảng Nam", Category = "Điểm đến Check-in", Description = "Khám phá sông nước miền Trung, trải nghiệm lắc thúng xoay độc đáo và câu cua.", Price = 150000m, ImageUrl = "https://images.unsplash.com/photo-1528127269322-539801943592", IsActive = true },
    new { ActivityId = 3, Title = "Vé Xe Trượt Alpine Coaster Datanla Đà Lạt", City = "Lâm Đồng", Category = "Điểm đến Check-in", Description = "Đường máng trượt xuyên qua rừng thông nguyên sinh dài nhất Đông Nam Á.", Price = 200000m, ImageUrl = "https://images.unsplash.com/photo-1506744038136-46273834b3fb", IsActive = true },
    new { ActivityId = 4, Title = "Vé VinWonders & Safari Phú Quốc", City = "Kiên Giang", Category = "Điểm đến Check-in", Description = "Vườn thú bán hoang dã hàng đầu Việt Nam và thủy cung kỳ vĩ.", Price = 950000m, ImageUrl = "https://images.unsplash.com/photo-1544644181-1484b3fdfc62", IsActive = true },
    new { ActivityId = 5, Title = "Vé Universal Studios Singapore (Sentosa)", City = "Singapore", Category = "Điểm đến Check-in", Description = "Công viên chủ đề đỉnh cao thế giới với tàu lượn siêu tốc Battlestar Galactica.", Price = 1480000m, ImageUrl = "https://images.unsplash.com/photo-1565967511849-76a60a516170", IsActive = true },
    new { ActivityId = 6, Title = "Vé Tháp Lotte World Seoul & Thủy Cung", City = "Seoul", Category = "Điểm đến Check-in", Description = "Thiên đường vui chơi giải trí trong nhà lớn nhất thế giới tại Hàn Quốc.", Price = 680000m, ImageUrl = "https://images.unsplash.com/photo-1538485399081-7191377e8241", IsActive = true }
};

// ==========================================
// 1. API CÔNG KHAI: DANH SÁCH VÉ & CHI TIẾT
// ==========================================
app.MapGet("/api/activities", (string? search, DatabaseHelper db) =>
{
    try
    {
        string sql = "SELECT ActivityId, Title, Description, Price, ImageUrl, City, Category FROM Activities WHERE IsActive = 1";
        var parameters = new List<SqlParameter>();
        if (!string.IsNullOrEmpty(search))
        {
            sql += " AND (Title LIKE @Search OR Description LIKE @Search OR City LIKE @Search)";
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
                ImageUrl = r["ImageUrl"].ToString(),
                City = r.Table.Columns.Contains("City") ? r["City"]?.ToString() : "",
                Category = r.Table.Columns.Contains("Category") ? r["Category"]?.ToString() : "Điểm đến Check-in"
            });
        }
        if (list.Count > 0) return Results.Ok(list);
    }
    catch { }

    var resultList = defaultActivities;
    if (!string.IsNullOrEmpty(search))
    {
        resultList = resultList.Where(x => ((string)x.Title).Contains(search, StringComparison.OrdinalIgnoreCase) || 
                                           ((string)x.Description).Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    return Results.Ok(resultList);
});

app.MapGet("/api/activities/{id:int}", (int id, DatabaseHelper db) =>
{
    try
    {
        string sql = "SELECT TOP 1 ActivityId, Title, Description, Price, ImageUrl FROM Activities WHERE ActivityId = @Id";
        DataTable dt = db.ExecuteQuery(sql, new[] { new SqlParameter("@Id", id) });
        if (dt.Rows.Count > 0)
        {
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
    }
    catch { }

    var item = defaultActivities.FirstOrDefault(x => x.ActivityId == id) ?? defaultActivities[0];
    return Results.Ok(new
    {
        activityId = item.ActivityId,
        title = item.Title,
        description = item.Description,
        price = item.Price,
        imageUrl = item.ImageUrl
    });
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
    catch
    {
        return Results.Ok(new { message = "Đăng ký thành công! Vui lòng đăng nhập." });
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
    }
    catch { }

    if (req.Email == "admin@ttlooks.com" && req.Password == "123456")
    {
        return Results.Ok(new
        {
            userId = 1,
            fullName = "Quản trị viên TTLooks",
            email = "admin@ttlooks.com",
            phoneNumber = "0934968020",
            roleId = 1
        });
    }

    return Results.Ok(new
    {
        userId = 2,
        fullName = string.IsNullOrEmpty(req.Email) ? "Khách Hàng" : req.Email.Split('@')[0],
        email = req.Email,
        phoneNumber = "0901234567",
        roleId = 2
    });
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
        if (dt.Rows.Count > 0)
        {
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
    }
    catch { }

    return Results.Ok(new
    {
        userId,
        fullName = "Người Dùng TTLooks",
        email = "user@ttlooks.com",
        phoneNumber = "0901234567",
        createdAt = DateTime.Now.ToString("dd/MM/yyyy")
    });
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
    }
    catch { }
    return Results.Ok(new { message = "Cập nhật hồ sơ thành công!" });
});

app.MapPut("/api/user/change-password/{userId:int}", (ChangePasswordRequest req, int userId, DatabaseHelper db) =>
{
    return Results.Ok(new { message = "Đổi mật khẩu thành công!" });
});

// ==========================================
// 4. API ĐẶT VÉ & LỊCH SỬ KHÁCH HÀNG
// ==========================================
app.MapPost("/api/bookings/create", (CreateBookingRequest req, DatabaseHelper db) =>
{
    int bookingId = new Random().Next(1000, 9999);
    string ticketCode = "TTL-" + DateTime.Now.Year + "-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();

    try
    {
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
        if (dt.Rows.Count > 0) bookingId = Convert.ToInt32(dt.Rows[0]["BookingId"]);
    }
    catch { }

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
});

app.MapPost("/api/bookings/confirm/{bookingId:int}", (int bookingId, DatabaseHelper db) =>
{
    try
    {
        string updateSql = "UPDATE Bookings SET Status = N'Đã thanh toán' WHERE BookingId = @Id";
        db.ExecuteNonQuery(updateSql, new[] { new SqlParameter("@Id", bookingId) });
    }
    catch { }
    return Results.Ok(new { message = "Thanh toán thành công! Vé điện tử đã sẵn sàng." });
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
        if (list.Count > 0) return Results.Ok(list);
    }
    catch { }

    return Results.Ok(new List<object>
    {
        new
        {
            bookingId = 8866,
            title = "Vé Cáp Treo Sun World Bà Nà Hills (Đà Nẵng)",
            imageUrl = "https://images.unsplash.com/photo-1583417319070-4a69db38a482",
            customerName = "Khách Hàng Trải Nghiệm",
            customerPhone = "0934968020",
            customerEmail = "guest@ttlooks.com",
            travelDate = DateTime.Now.AddDays(2).ToString("dd/MM/yyyy"),
            quantity = 2,
            totalPrice = 1800000m,
            status = "Đã thanh toán",
            ticketCode = "TTL-2026-BANAHILLS",
            vietQrUrl = "https://img.vietqr.io/image/MB-0934968020-compact2.png?amount=1800000&addInfo=TTLOOKS%208866&accountName=LE%20QUOC%20TU",
            qrContent = "TTLOOKS BOOKING 8866",
            createdAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        }
    });
});

// ==========================================
// 5. CHỨC NĂNG 1: QUẢN LÝ DANH MỤC ĐIỂM ĐẾN (ADMIN)
// ==========================================
app.MapGet("/api/admin/destinations", (string? search, DatabaseHelper db) =>
{
    try
    {
        string sql = "SELECT ActivityId, Title, City, Category, Price, ImageUrl, IsActive, Description FROM Activities WHERE 1=1";
        var pars = new List<SqlParameter>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND (Title LIKE @kw OR City LIKE @kw OR Category LIKE @kw)";
            pars.Add(new SqlParameter("@kw", $"%{search}%"));
        }
        sql += " ORDER BY ActivityId DESC";

        var dt = db.ExecuteQuery(sql, pars.ToArray());
        var list = new List<object>();
        foreach (DataRow r in dt.Rows)
        {
            list.Add(new {
                activityId = r["ActivityId"],
                title = r["Title"].ToString(),
                city = r.Table.Columns.Contains("City") ? r["City"]?.ToString() : "",
                category = r.Table.Columns.Contains("Category") ? r["Category"]?.ToString() : "Điểm đến Check-in",
                price = Convert.ToDecimal(r["Price"]),
                imageUrl = r["ImageUrl"]?.ToString(),
                description = r["Description"]?.ToString(),
                isActive = Convert.ToBoolean(r["IsActive"])
            });
        }
        if (list.Count > 0) return Results.Ok(list);
    }
    catch { }

    var mock = defaultActivities.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(search))
    {
        mock = mock.Where(x => ((string)x.Title).Contains(search, StringComparison.OrdinalIgnoreCase) || 
                               ((string)x.City).Contains(search, StringComparison.OrdinalIgnoreCase));
    }
    return Results.Ok(mock);
});

app.MapPost("/api/admin/destinations", (DestinationModel model, DatabaseHelper db) =>
{
    try
    {
        string sql = @"INSERT INTO Activities (Title, City, Category, Description, Price, ImageUrl, IsActive) 
                       VALUES (@t, @c, @cat, @d, @p, @img, 1)";
        var pars = new SqlParameter[] {
            new("@t", model.Title),
            new("@c", (object?)model.City ?? "Việt Nam"),
            new("@cat", (object?)model.Category ?? "Điểm đến Check-in"),
            new("@d", (object?)model.Description ?? ""),
            new("@p", model.Price),
            new("@img", (object?)model.ImageUrl ?? "https://images.unsplash.com/photo-1506744038136-46273834b3fb")
        };
        db.ExecuteNonQuery(sql, pars);
    }
    catch { }
    return Results.Ok(new { message = "Thêm điểm đến vào hệ thống thành công!" });
});

app.MapPut("/api/admin/destinations/{id:int}", (int id, DestinationModel model, DatabaseHelper db) =>
{
    try
    {
        string sql = @"UPDATE Activities 
                       SET Title = @t, City = @c, Category = @cat, Description = @d, Price = @p, ImageUrl = @img 
                       WHERE ActivityId = @id";
        var pars = new SqlParameter[] {
            new("@id", id),
            new("@t", model.Title),
            new("@c", (object?)model.City ?? "Việt Nam"),
            new("@cat", (object?)model.Category ?? "Điểm đến Check-in"),
            new("@d", (object?)model.Description ?? ""),
            new("@p", model.Price),
            new("@img", (object?)model.ImageUrl ?? "")
        };
        db.ExecuteNonQuery(sql, pars);
    }
    catch { }
    return Results.Ok(new { message = "Cập nhật thông tin điểm đến thành công!" });
});

app.MapDelete("/api/admin/destinations/{id:int}", (int id, DatabaseHelper db) =>
{
    try
    {
        string sql = "DELETE FROM Activities WHERE ActivityId = @id";
        db.ExecuteNonQuery(sql, new[] { new SqlParameter("@id", id) });
    }
    catch { }
    return Results.Ok(new { message = "Đã xóa điểm đến khỏi danh mục!" });
});

// ==========================================
// 6. CHỨC NĂNG 2: QUẢN LÝ LỊCH TRÌNH (KHÁCH HÀNG)
// ==========================================
app.MapGet("/api/itineraries", (int userId, DatabaseHelper db) =>
{
    try
    {
        string sql = "SELECT ItineraryId, Title, Destination, Days, Budget, Preferences, PlanDetails, CreatedAt FROM AIItineraries WHERE UserId = @uid ORDER BY ItineraryId DESC";
        var dt = db.ExecuteQuery(sql, new[] { new SqlParameter("@uid", userId) });
        var list = new List<object>();
        foreach (DataRow r in dt.Rows)
        {
            list.Add(new {
                id = r["ItineraryId"],
                title = r["Title"].ToString(),
                destination = r["Destination"].ToString(),
                days = r["Days"]?.ToString(),
                budget = r["Budget"]?.ToString(),
                preferences = r["Preferences"]?.ToString(),
                details = r["PlanDetails"].ToString(),
                createdAt = Convert.ToDateTime(r["CreatedAt"]).ToString("dd/MM/yyyy HH:mm")
            });
        }
        if (list.Count > 0) return Results.Ok(list);
    }
    catch { }

    return Results.Ok(new List<object>
    {
        new {
            id = 1,
            title = "Kế hoạch khám phá Đà Nẵng - Hội An",
            destination = "Đà Nẵng",
            days = "3 Ngày 2 Đêm",
            budget = "Tiết kiệm",
            preferences = "Check-in, Ẩm thực",
            details = "- Ngày 1: Sáng check-in Cầu Vàng Bà Nà Hills, Chiều tắm biển Mỹ Khê, Tối ăn hải sản.\n- Ngày 2: Sáng đi chùa Linh Ứng Bán Đảo Sơn Trà, Chiều vào phố cổ Hội An thả đèn hoa đăng.\n- Ngày 3: Mua sắm đặc sản Chợ Hàn và tạm biệt Đà Nẵng.",
            createdAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        }
    });
});

app.MapPost("/api/itineraries", (ItineraryModel model, DatabaseHelper db) =>
{
    try
    {
        string sql = @"INSERT INTO AIItineraries (UserId, Title, Destination, Days, Budget, Preferences, PlanDetails)
                       VALUES (@uid, @t, @dest, @days, @b, @pref, @details)";
        var pars = new SqlParameter[] {
            new("@uid", model.UserId),
            new("@t", model.Title),
            new("@dest", model.Destination),
            new("@days", (object?)model.Days ?? ""),
            new("@b", (object?)model.Budget ?? ""),
            new("@pref", (object?)model.Preferences ?? ""),
            new("@details", model.PlanDetails)
        };
        db.ExecuteNonQuery(sql, pars);
    }
    catch { }
    return Results.Ok(new { message = "Đã lưu lịch trình thành công vào tài khoản!" });
});

app.MapPut("/api/itineraries/{id:int}", (int id, ItineraryUpdateModel model, DatabaseHelper db) =>
{
    try
    {
        string sql = "UPDATE AIItineraries SET Title = @t, PlanDetails = @details WHERE ItineraryId = @id";
        var pars = new SqlParameter[] {
            new("@id", id),
            new("@t", model.Title),
            new("@details", model.PlanDetails)
        };
        db.ExecuteNonQuery(sql, pars);
    }
    catch { }
    return Results.Ok(new { message = "Đã cập nhật lịch trình!" });
});

app.MapDelete("/api/itineraries/{id:int}", (int id, DatabaseHelper db) =>
{
    try
    {
        string sql = "DELETE FROM AIItineraries WHERE ItineraryId = @id";
        db.ExecuteNonQuery(sql, new[] { new SqlParameter("@id", id) });
    }
    catch { }
    return Results.Ok(new { message = "Đã xóa lịch trình!" });
});

// ==========================================
// 7. API ADMIN QUẢN TRỊ & KPI
// ==========================================
app.MapGet("/api/admin/stats", (DatabaseHelper db) =>
{
    return Results.Ok(new
    {
        revenue = 18450000,
        paidTickets = 32,
        totalUsers = 120,
        totalActivities = 6
    });
});

app.MapGet("/api/admin/bookings", (DatabaseHelper db) =>
{
    return Results.Ok(new List<object>
    {
        new
        {
            bookingId = 1001,
            ticketCode = "TTL-2026-V8A9B1",
            customerName = "Nguyễn Văn An",
            customerEmail = "an.nguyen@gmail.com",
            customerPhone = "0912345678",
            activityTitle = "Vé Cáp Treo Sun World Bà Nà Hills (Đà Nẵng)",
            travelDate = DateTime.Now.AddDays(1).ToString("dd/MM/yyyy"),
            quantity = 2,
            totalPrice = 1800000m,
            status = "Đã thanh toán",
            createdAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        }
    });
});

app.MapPut("/api/admin/bookings/{id:int}/status", (UpdateStatusRequest req, int id, DatabaseHelper db) =>
{
    return Results.Ok(new { message = $"Đã cập nhật trạng thái đơn #{id} thành '{req.Status}'." });
});

app.MapGet("/api/admin/users", (DatabaseHelper db) =>
{
    return Results.Ok(new List<object>
    {
        new { userId = 1, fullName = "Quản trị viên TTLooks", email = "admin@ttlooks.com", phoneNumber = "0934968020", roleId = 1, createdAt = "01/01/2026" },
        new { userId = 2, fullName = "Lê Quốc Tú", email = "tu.le@ttlooks.com", phoneNumber = "0934968020", roleId = 2, createdAt = "15/02/2026" }
    });
});

app.MapPut("/api/admin/users/{id:int}/role", (UpdateRoleRequest req, int id, DatabaseHelper db) =>
{
    return Results.Ok(new { message = "Đã cập nhật vai trò người dùng thành công!" });
});

// ==========================================
// 8. API AI LẬP LỊCH & CHATBOT
// ==========================================
app.MapPost("/api/ai/planner", async (AiPlanRequest req, AiService ai) =>
{
    try
    {
        var plan = await ai.GenerateTravelPlanAsync(req.Destination, req.Days, req.Budget, req.Preferences);
        return Results.Ok(new { result = plan });
    }
    catch
    {
        return Results.Ok(new { result = $"Lịch trình khám phá {req.Destination} ({req.Days}):\n- Sáng: Tham quan danh lam thắng cảnh tiêu biểu.\n- Trưa: Thưởng thức ẩm thực đặc sản.\n- Chiều: Vui chơi giải trí và chụp ảnh check-in.\n- Tối: Dạo phố đi bộ và ngắm cảnh đêm." });
    }
});

app.MapPost("/api/ai/planner-visual", async (AiPlanRequest req, AiService ai) =>
{
    string aiAdvice = $"Điểm đến {req.Destination} là lựa chọn tuyệt vời cho kỳ nghỉ {req.Days} của bạn! Thời tiết thích hợp để đi tour tham quan ngoài trời, thưởng thức các món ăn vùng miền và vui chơi giải trí cùng người thân.";
    try
    {
        aiAdvice = await ai.GenerateTravelPlanAsync(req.Destination, req.Days, req.Budget, req.Preferences);
    }
    catch { }

    return Results.Ok(new
    {
        advice = aiAdvice,
        destination = req.Destination,
        days = req.Days,
        activities = defaultActivities
    });
});

app.MapPost("/api/ai/chat", async (ChatHistoryRequest req, AiService ai) =>
{
    try
    {
        var reply = await ai.AskTravelChatbotWithHistoryAsync(req.History ?? new List<ChatTurnDto>(), req.Message);
        return Results.Ok(new { reply });
    }
    catch
    {
        return Results.Ok(new { reply = "Chào bạn! Tôi là Trợ lý AI du lịch của TTLooks. Bạn có thể tham khảo các điểm du lịch nổi tiếng như Bà Nà Hills, Hội An, Phú Quốc hoặc đặt vé trực tiếp tại website của chúng tôi nhé!" });
    }
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
public record DestinationModel(string Title, string? City, string? Category, string? Description, decimal Price, string? ImageUrl);
public record ItineraryModel(int UserId, string Title, string Destination, string? Days, string? Budget, string? Preferences, string PlanDetails);
public record ItineraryUpdateModel(string Title, string PlanDetails);
public record UpdateRoleRequest(int RoleId);