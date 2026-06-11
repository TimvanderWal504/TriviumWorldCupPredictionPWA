self.addEventListener('push', function (event) {
  if (!event.data) return;
  var data;
  try { data = event.data.json(); } catch (_) { data = {}; }
  var title = data.title || 'TWC 2026';
  var body  = data.body  || 'You have a new notification.';
  event.waitUntil(
    self.registration.showNotification(title, {
      body:  body,
      icon:  '/icons/icon-192.png',
      badge: '/icons/icon-192.png',
    })
  );
});

self.addEventListener('notificationclick', function (event) {
  event.notification.close();
  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (list) {
      for (var i = 0; i < list.length; i++) {
        if (list[i].url.startsWith(self.location.origin) && 'focus' in list[i]) {
          return list[i].focus();
        }
      }
      return clients.openWindow('/');
    })
  );
});
