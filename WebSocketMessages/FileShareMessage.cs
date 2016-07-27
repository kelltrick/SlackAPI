namespace SlackAPI.WebSocketMessages
{
    public class FileShareMessage : NewMessage
    {
        [SlackSocketRouting("message", "file_share")]
        public FileShareMessage()
        { }

        public bool upload;
        public File file;
    }
}
