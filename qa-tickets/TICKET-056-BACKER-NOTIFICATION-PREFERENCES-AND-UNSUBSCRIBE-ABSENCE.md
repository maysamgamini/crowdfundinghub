# QA Ticket: TICKET-056

**Title:** Missing Backer Notification Preferences & Unsubscribe Enforcement: Mandatory Transactional vs. Commercial Email Segregation  
**Severity:** 🟡 P2 (Medium - Legal Compliance, GDPR / CAN-SPAM & User Privacy)  
**QA Focus Area:** Notifications Architecture, Compliance & Preference Enforcement  
**Found By:** `qa-api-contract-compliance` & `qa-functional-domain`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In [TICKET-031](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/qa-tickets/TICKET-031-EXTERNAL-EMAIL-NOTIFICATION-OUTBOX-DELIVERY.md), transactional email delivery was added to the `Notifications` module via [`NotificationEventHandlers.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Notifications/CrowdFunding.Modules.Notifications.Application/Events/NotificationEventHandlers.cs), executing transactional sends for contribution receipts and campaign refund notices.

### The Architectural Defect

As the platform evolves to support campaign activity notifications, creator announcements, and backer engagement:
1. **Absence of Preference Aggregate:** The `Notifications` module possesses no `NotificationPreference` aggregate or table. Every registered user is treated as having identical notification configurations.
2. **Missing Preferences API:** There are no endpoints allowing backers to inspect or modify their notification preferences (`GET /api/notifications/preferences`, `PUT /api/notifications/preferences`).
3. **Lack of Category Classification (Transactional vs. Commercial):** The email dispatching pipeline does not differentiate between:
   - **Transactional Notifications:** Legal and financial receipts (pledge confirmation, refund notices, account verification) which must bypass opt-out rules.
   - **Commercial / Engagement Notifications:** Campaign updates, creator newsletters, stretch goal announcements, and marketing updates, which legally require explicit consent and an unsubscribe mechanism under CAN-SPAM Act, CASL, and EU GDPR (Directive 2002/58/EC).
4. **Missing `List-Unsubscribe` Headers:** Outgoing emails lack RFC 2369 / RFC 8058 `List-Unsubscribe` and `List-Unsubscribe-Post` headers, leading major email providers (Gmail, Yahoo, Outlook) to penalize domain sender reputation and route messages to spam folders.

---

## 2. Blast Radius & Legal / Reputational Impact

- **Regulatory Non-Compliance:** Disagreeing with user communication preferences or failing to offer an unsubscribe mechanism violates GDPR Article 7, CAN-SPAM (15 U.S.C. 7704), and CASL, risking administrative fines.
- **Sender Reputation Degradation:** When users cannot opt out of non-critical campaign updates, they click "Report Spam" in their email client. A spam rate exceeding 0.3% triggers bulk sender blacklisting by Google and Microsoft, degrading deliverability even for critical financial receipts.
- **Architectural Coupling:** Handlers currently call `IEmailNotificationService` directly without passing through a preference policy interceptor or domain filter.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach architects the **Notification Policy Interceptor Pattern** and **Compliance Classification**:
> *Never deliver non-transactional messages directly to external gateways without passing through a user consent policy pipeline.*

Architecturally, notification delivery systems must classify all outgoing dispatches into `Transactional` vs `Commercial/Informational`. While transactional emails are non-negotiable for system correctness, commercial notifications must be filtered against a low-latency preference store before hitting external email provider APIs.

---

## 4. Affected Files & Modules

- [`src/Modules/Notifications/CrowdFunding.Modules.Notifications.Domain/Aggregates/NotificationPreference.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Notifications/CrowdFunding.Modules.Notifications.Domain/) *(New Aggregate)*
- [`src/Modules/Notifications/CrowdFunding.Modules.Notifications.Application/Events/NotificationEventHandlers.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Notifications/CrowdFunding.Modules.Notifications.Application/Events/NotificationEventHandlers.cs)
- [`src/Modules/Notifications/CrowdFunding.Modules.Notifications.Application/Abstractions/Services/IEmailNotificationService.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Notifications/CrowdFunding.Modules.Notifications.Application/Abstractions/Services/IEmailNotificationService.cs)
- [`src/API/CrowdFunding.API/Controllers/NotificationsController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/) *(New Controller)*

---

## 5. Greenfield Remediation Guidance

### Step 1: Create `NotificationPreference` Aggregate in `Notifications.Domain`

```csharp
public sealed class NotificationPreference
{
    public Guid UserId { get; private set; }
    public bool CampaignUpdatesEnabled { get; private set; }
    public bool MarketingAnnouncementsEnabled { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static NotificationPreference CreateDefault(Guid userId, DateTime nowUtc)
        => new(userId, campaignUpdatesEnabled: true, marketingAnnouncementsEnabled: false, nowUtc);

    public void Update(bool campaignUpdates, bool marketing, DateTime nowUtc)
    {
        CampaignUpdatesEnabled = campaignUpdates;
        MarketingAnnouncementsEnabled = marketing;
        UpdatedAtUtc = nowUtc;
    }
}
```

### Step 2: Decorate Email Gateway with Preference Enforcement

Introduce a `NotificationCategory` enum (`Transactional`, `CampaignUpdate`, `Commercial`):
- For `Transactional`: Dispatch immediately regardless of preferences.
- For `CampaignUpdate` or `Commercial`: Query user preferences before calling external email delivery; if disabled, drop or skip delivery cleanly with an audit log.

### Step 3: Expose REST Endpoints for User Preferences

Add `NotificationsController`:
- `GET /api/notifications/preferences` — returns current authenticated user's communication preferences.
- `PUT /api/notifications/preferences` — updates preferences.
- Support cryptographic one-click unsubscribe links via signed query tokens (`GET /api/notifications/unsubscribe?token=...`).

---

## 6. Verification & Acceptance Criteria

1. **Transactional Guaranteed:** Pledge receipts and refund confirmations are dispatched regardless of opt-out settings.
2. **Opt-Out Enforced:** Users who set `CampaignUpdatesEnabled = false` do not receive creator update emails.
3. **RFC 8058 Header Presence:** Outgoing commercial emails contain compliant `List-Unsubscribe` headers.
4. **Preference CRUD:** Backers can fetch and update their preferences via authenticated REST endpoints.
