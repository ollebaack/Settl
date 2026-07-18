/// <reference types="nativewind/types" />

// The global stylesheet is imported for its side effect (Metro's NativeWind transformer
// turns the @tailwind directives into styles); TS needs it declared as a module.
declare module '*.css';
