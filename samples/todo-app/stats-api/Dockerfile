FROM mcr.microsoft.com/cbl-mariner/base/nodejs:18.17.1-2-cm2.0.20231004@sha256:4967b4998350f5e438a282c9c40952e182fb744df8d05eae43ff7f62fb267fdf
ENV PORT 80
EXPOSE 80

WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .

CMD ["npm", "start"]