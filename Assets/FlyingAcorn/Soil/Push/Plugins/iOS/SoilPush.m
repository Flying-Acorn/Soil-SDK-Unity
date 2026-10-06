// Push.ClearDelivered() on iOS: removes this app's delivered notifications from Notification Center.
// Objective-C (not ++) so the UserNotifications import links its framework on its own.
@import UserNotifications;

void _SoilPushClearDelivered(void)
{
    [[UNUserNotificationCenter currentNotificationCenter] removeAllDeliveredNotifications];
}
