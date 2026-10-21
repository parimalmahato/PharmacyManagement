
import { useCallback, useEffect, useState } from "react";

import {
  notificationsApi,
  type PharmacyNotification,
  type NotificationSummary,
} from "../api/notifications";

import "./NotificationsPage.css";

export default function NotificationsPage() {
  const [notifications, setNotifications] =
    useState<PharmacyNotification[]>([]);

  const [summary, setSummary] =
    useState<NotificationSummary | null>(null);

  const [unreadOnly, setUnreadOnly] = useState(false);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  const loadNotifications = useCallback(
    async (showUnreadOnly: boolean) => {
      setLoading(true);
      setError("");

      try {
        const [items, summaryData] = await Promise.all([
          notificationsApi.getAll(showUnreadOnly),
          notificationsApi.getSummary(),
        ]);

        setNotifications(items);
        setSummary(summaryData);
      } catch (err) {
        setError(
          err instanceof Error
            ? err.message
            : "Unable to load notifications."
        );
      } finally {
        setLoading(false);
      }
    },
    []
  );

  useEffect(() => {
    Promise.resolve().then(() => {
    void loadNotifications(unreadOnly);
    });
  }, [loadNotifications, unreadOnly]);

  async function handleMarkAsRead(id: number) {
    setBusy(true);
    setError("");
    setMessage("");

    try {
      await notificationsApi.markAsRead(id);
      setMessage("Notification marked as read.");
      await loadNotifications(unreadOnly);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to update notification."
      );
    } finally {
      setBusy(false);
    }
  }

  async function handleMarkAllAsRead() {
    setBusy(true);
    setError("");
    setMessage("");

    try {
      const updatedCount =
        await notificationsApi.markAllAsRead();

      setMessage(
        `${updatedCount} notification(s) marked as read.`
      );

      await loadNotifications(unreadOnly);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to update notifications."
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="notifications-page">
      <div className="notifications-heading">
        <div>
          <h1>Notification Center</h1>
          <p>
            Review medicine expiry alerts and updates.
          </p>
        </div>

        <button
          type="button"
          onClick={() => void loadNotifications(unreadOnly)}
          disabled={loading || busy}
        >
          Refresh
        </button>
      </div>

      {error && (
        <div className="notifications-error" role="alert">
          {error}
        </div>
      )}

      {message && (
        <div className="notifications-success">
          {message}
        </div>
      )}

      <div className="notifications-summary">
        <div>
          <span>Total Notifications</span>
          <strong>{summary?.totalCount ?? 0}</strong>
        </div>

        <div>
          <span>Unread Notifications</span>
          <strong>{summary?.unreadCount ?? 0}</strong>
        </div>
      </div>

      <div className="notifications-toolbar">
        <label>
          <input
            type="checkbox"
            checked={unreadOnly}
            onChange={(e) =>
              setUnreadOnly(e.target.checked)
            }
          />
          Unread only
        </label>

        <button
          type="button"
          onClick={() => void handleMarkAllAsRead()}
          disabled={
            busy ||
            loading ||
            (summary?.unreadCount ?? 0) === 0
          }
        >
          Mark All as Read
        </button>
      </div>

      {loading && <p>Loading notifications...</p>}

      <div className="notifications-list">
        {!loading && notifications.length === 0 && (
          <p>No notifications found.</p>
        )}

        {notifications.map((notification) => (
          <div
            key={notification.id}
            className={`notification-card ${
              notification.isRead ? "read" : "unread"
            }`}
          >
            <div className="notification-card-header">
              <strong>{notification.alertType}</strong>

              {!notification.isRead && (
                <span className="notification-new-badge">
                  New
                </span>
              )}
            </div>

            <p>{notification.message}</p>

            <div className="notification-card-footer">
              <small>
                {new Date(
                  notification.createdAtUtc
                ).toLocaleString("en-IN", {
                  timeZone: "Asia/Kolkata",
                  dateStyle: "medium",
                  timeStyle: "short",
                })}
              </small>

              {!notification.isRead && (
                <button
                  type="button"
                  onClick={() =>
                    void handleMarkAsRead(notification.id)
                  }
                  disabled={busy}
                >
                  Mark as Read
                </button>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
