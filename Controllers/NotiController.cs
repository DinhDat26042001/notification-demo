using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NotificationAPI.Models;
using Microsoft.Data.SqlClient;
namespace NotificationAPI.Controllers
{
    [Route("api/notification")]
    [ApiController]
    public class NotiController : ControllerBase
    {
        private readonly IConfiguration _config;
          public NotiController(IConfiguration config)
    {
        _config = config;
    }
        [HttpGet("notifications/{userId}")]
        public async Task<IActionResult> GetNotificationsByUserId(int userId)
        {
            if (userId <= 0)
            {
                return BadRequest("UserID không hợp lệ.");
            }

            List<NotificationResponse> notifications = new List<NotificationResponse>();

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("MyDB")))
            {
                await conn.OpenAsync();

                string query = @"
            SELECT ID, TieuDe, NoiDung, Url, IsDaXem, IsDaGui, IDPhieu, NgayTao
            FROM NotiAPI_Notification
            WHERE UserId = @UserId
            ORDER BY NgayTao DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            notifications.Add(new NotificationResponse
                            {
                                ID = reader.GetInt32(0),
                                TieuDe = reader.GetString(1),
                                NoiDung = reader.GetString(2),
                                Url = reader.IsDBNull(3) ? null : reader.GetString(3),
                                IsDaXem = reader.GetBoolean(4),
                                IsDaGui = reader.GetBoolean(5),
                                IDPhieu = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                                NgayTao = reader.GetDateTime(7)
                            });
                        }
                    }
                }
            }
            return Ok(notifications);
        }
        [HttpPut("read-notification/{id}")]
        public async Task<IActionResult> ReadNotification(int id)
        {
            if (id <= 0)
            {
                return BadRequest("ID thông báo không hợp lệ.");
            }

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("MyDB")))
            {
                await conn.OpenAsync();
                string query = "UPDATE NotiAPI_Notification SET IsDaXem = 1 WHERE ID = @ID";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ID", id);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        return NotFound("Không tìm thấy thông báo.");
                    }
                }
            }

            return Ok(new { message = "Thông báo đã được đọc." });
        }
      
    }
}
