// babel-preset-expo still handles Reanimated/Worklets and the React Compiler (app.json).
// NativeWind adds its own JSX runtime (jsxImportSource) plus the nativewind/babel preset so
// `className` props compile to styles.
module.exports = function (api) {
  api.cache(true);
  return {
    presets: [
      ['babel-preset-expo', { jsxImportSource: 'nativewind' }],
      'nativewind/babel',
    ],
  };
};
