namespace InhabitantChess.API
{
    public class InhabitantChessAPI : IInhabitantChessAPI
    {
        public string TestAPI(string message) => $"Your message: {message}";
    }
}
