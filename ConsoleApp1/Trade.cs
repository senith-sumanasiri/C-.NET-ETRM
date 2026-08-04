
public record Trade(
    int TradeID, 
    string Party, 
    string CounterParty, 
    string Commodity, 
    double Volume, 
    double Price, 
    string DeliveryMonth, 
    string BuySell
);