using Marketplace.Api.Data;

namespace Marketplace.Api.Domain;

public enum OrderAction
{
    Accept,
    Deliver,
    Complete,
    Cancel
}

public enum OrderParty
{
    Buyer,
    Seller
}

public enum TransitionDecision
{
    Allowed,
    ActorNotAllowed,
    WrongStatus
}

public record TransitionResult(TransitionDecision Decision, OrderStatus? NewStatus);

// The order state machine (data-model.md, section 3).
// Plain code with no database, so it is easy to read and to test.
public static class OrderRules
{
    private record Rule(OrderStatus From, OrderStatus To, OrderParty[] Allowed);

    private static readonly Dictionary<OrderAction, Rule> Rules = new()
    {
        [OrderAction.Accept] = new(OrderStatus.Pending, OrderStatus.InProgress, [OrderParty.Seller]),
        [OrderAction.Deliver] = new(OrderStatus.InProgress, OrderStatus.Delivered, [OrderParty.Seller]),
        [OrderAction.Complete] = new(OrderStatus.Delivered, OrderStatus.Completed, [OrderParty.Buyer]),
        [OrderAction.Cancel] = new(OrderStatus.Pending, OrderStatus.Cancelled, [OrderParty.Buyer, OrderParty.Seller])
    };

    public static TransitionResult Decide(OrderStatus current, OrderAction action, OrderParty actor)
    {
        var rule = Rules[action];

        if (!rule.Allowed.Contains(actor))
            return new TransitionResult(TransitionDecision.ActorNotAllowed, null);

        if (current != rule.From)
            return new TransitionResult(TransitionDecision.WrongStatus, null);

        return new TransitionResult(TransitionDecision.Allowed, rule.To);
    }
}