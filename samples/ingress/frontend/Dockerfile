FROM node:lts-alpine@sha256:be8f26834660fd887d22fb2659615f3ef2dd9eaf2a3d544512674eeb7959cafb
ENV PORT 80
EXPOSE 80

WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build

CMD ["npm", "start"]