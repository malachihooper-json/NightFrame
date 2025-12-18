import type { Metadata } from "next";
import { Inter, JetBrains_Mono } from "next/font/google";
import "./globals.css";

const inter = Inter({
  subsets: ["latin"],
  variable: "--font-inter",
});

const jetbrainsMono = JetBrains_Mono({
  subsets: ["latin"],
  variable: "--font-mono",
});

export const metadata: Metadata = {
  title: "NIGHTFRAME | Decentralized AI Mesh Console",
  description: "Command and control interface for the NIGHTFRAME decentralized AI compute mesh. Monitor drones, submit prompts, and track the neural network economy.",
  keywords: ["AI", "mesh network", "decentralized", "neural network", "distributed computing"],
  authors: [{ name: "Project NIGHTFRAME" }],
  themeColor: "#7c3aed",
  openGraph: {
    title: "NIGHTFRAME Console",
    description: "Decentralized AI Mesh Control Interface",
    type: "website",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className={`${inter.variable} ${jetbrainsMono.variable}`}>
      <head>
        <link rel="icon" href="/favicon.ico" />
      </head>
      <body className="antialiased">
        {children}
      </body>
    </html>
  );
}
