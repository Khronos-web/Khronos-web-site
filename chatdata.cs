using System.Collections.Generic;
 
namespace Codec;
 
public class ChatsData
{
    public int Counter { get; set; }
    public int Selected { get; set; }
    public List<SavedChat> Chats { get; set; } = new();
}
 
public class SavedChat
{
    public string Title { get; set; } = string.Empty;
    public List<string> Messages { get; set; } = new();
}
 