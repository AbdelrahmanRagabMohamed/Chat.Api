﻿

namespace ChatApi.Models;
public class Conversation
{
    public int Id { get; set; }
    public int User1Id { get; set; }
    public User User1 { get; set; }
    public int User2Id { get; set; }
    public User User2 { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted_ForUser_1 { get; set; } = false; // إضافة خاصية للحذف من User1
    public bool IsDeleted_ForUser_2 { get; set; } = false; // إضافة خاصية للحذف من User2
    public List<Message> Messages { get; set; } = new List<Message>(); // التأكد إن المجموعة مبيجيش null
}