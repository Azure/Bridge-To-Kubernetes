import App, {Container} from 'next/app'
import Head from 'next/head'
import React from 'react'
import './index.css'

export default class MyApp extends App {
  static async getInitialProps ({ Component, ctx }) {
    let pageProps = {};

    if (Component.getInitialProps) {
      pageProps = await Component.getInitialProps(ctx);
    }

    return { pageProps };
  }

  render () {
    const { Component, pageProps } = this.props;

    return (
      <div>
        <Head>
            <title>Todo App Sample</title>
        </Head>
        <Component {...pageProps} />
      </div>
    );
  }
}