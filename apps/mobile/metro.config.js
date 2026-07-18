// Expo's default Metro config has built-in pnpm-monorepo support (SDK 54+), so no manual
// watchFolders / node_modules wiring is needed here. withNativeWind layers Tailwind on top,
// pointing at the global stylesheet that carries the @tailwind directives.
const { getDefaultConfig } = require('expo/metro-config');
const { withNativeWind } = require('nativewind/metro');

const config = getDefaultConfig(__dirname);

module.exports = withNativeWind(config, { input: './src/global.css' });
