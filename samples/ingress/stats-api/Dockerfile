FROM node:lts-alpine@sha256:c0a3badbd8a0a760de903e00cedbca94588e609299820557e72cba2a53dbaa2c
ENV PORT 80
EXPOSE 80

WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .

CMD ["npm", "start"]