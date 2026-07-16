#!/usr/bin/with-contenv bash
# Rebrand Jitsi web UI -> SpaceMeet (runs after 10-config regenerates config)
F=/config/interface_config.js
if [ -f "$F" ]; then
  sed -i "s/APP_NAME: '[^']*'/APP_NAME: 'SpaceMeet'/" "$F"
  sed -i "s/PROVIDER_NAME: '[^']*'/PROVIDER_NAME: 'SpaceMeet'/" "$F"
  sed -i "s/SHOW_JITSI_WATERMARK: *true/SHOW_JITSI_WATERMARK: false/" "$F"
  sed -i "s/SHOW_WATERMARK_FOR_GUESTS: *true/SHOW_WATERMARK_FOR_GUESTS: false/" "$F"
  sed -i "s/SHOW_POWERED_BY: *true/SHOW_POWERED_BY: false/" "$F"
  sed -i "s/SHOW_BRAND_WATERMARK: *true/SHOW_BRAND_WATERMARK: false/" "$F"
fi
# static <title> and meta in the served HTML
for H in /usr/share/jitsi-meet/index.html /usr/share/jitsi-meet/title.html; do
  [ -f "$H" ] && sed -i "s/Jitsi Meet/SpaceMeet/g" "$H"
done
