# Real-Time Notifications

## Purpose
Provides WebSocket and Server-Sent Events push notifications using ASP.NET Core SignalR, allowing connected clients to receive immediate live funding updates without polling.

## Files
- `CampaignHub.cs`: Strongly mapped SignalR hub hosted at `/hubs/campaigns`. Exposes `JoinCampaign(campaignId)` and `LeaveCampaign(campaignId)` group membership methods so backers viewing a campaign page only receive push updates relevant to that campaign.
- `SignalRCampaignRealtimeNotifier.cs`: Implements the `ICampaignRealtimeNotifier` abstraction defined in `Campaigns.Application`. Dispatches `PledgeReceived` messages (`campaignId`, `raisedAmount`, `currency`) to the associated SignalR group whenever the raised amount changes (a pledge credited or a refund recorded), and `RewardTierSoldOut` messages when a reward tier's last slot is taken. With a Redis connection string configured, the Redis backplane delivers broadcasts across instances. Failures are treated as best-effort telemetry and swallowed so transport hiccups never roll back confirmed pledges.

## Client Integration
1. **Connect to Hub**:
   ```javascript
   const connection = new signalR.HubConnectionBuilder()
       .withUrl("/hubs/campaigns")
       .withAutomaticReconnect()
       .build();
   await connection.start();
   ```

2. **Subscribe to Campaign**:
   ```javascript
   await connection.invoke("JoinCampaign", campaignId);
   ```

3. **Listen for Events**:
   ```javascript
   connection.on("PledgeReceived", (data) => {
       console.log(`Campaign ${data.campaignId} raised ${data.raisedAmount} ${data.currency}`);
   });
   ```
